using CompilerCommon;
using ILReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Assembler
{
    public partial class ProgramBuilder
    {
        private const int RegisterCount = 32;
        private const int RamAddress = 16385;
        private const int HeapAddress = RamAddress + 1024;
        private const int ReturnRegister = 3;

        public List<Instruction> Instructions { get; } = new List<Instruction>();
        public List<Dictionary<int, int>> Data { get; } = new List<Dictionary<int, int>>();
        public List<string> Errors { get; } = new List<string>();

        private MethodContext methodContext = new MethodContext { InstructionIndex = 0 };
        private readonly Dictionary<MethodBase, MethodContext> methodContexts = new Dictionary<MethodBase, MethodContext>();
        private readonly Dictionary<MethodBase, MethodAnalysis> methodAnalyses = new Dictionary<MethodBase, MethodAnalysis>();
        private readonly Dictionary<MethodBase, List<(int, int)>> methodRanges = new Dictionary<MethodBase, List<(int, int)>>();
        private HashSet<MethodBase> nonInlinedMethods;
        private readonly HashSet<Type> types = new HashSet<Type>();
        private readonly Dictionary<Type, TypeInfo> typeInfoCache = new Dictionary<Type, TypeInfo>();
        private readonly Dictionary<MethodBase, MethodParameterInfo> methodParameterInfoCache = new Dictionary<MethodBase, MethodParameterInfo>();
        private readonly Dictionary<FieldInfo, VariableInfo> staticFields = new Dictionary<FieldInfo, VariableInfo>();
        private readonly List<(Instruction, MethodBase)> calls = new List<(Instruction, MethodBase)>();
        private int initialStackPointer;

        public ProgramBuilder()
        {
            InitializeCompilerGeneratedMethods();
        }

        public void Build(Assembly assembly, StreamWriter instructionsWriter)
        {
            var main = assembly.EntryPoint;

            Analyze(main);
            AllocateStaticFields();
            EmitTypes();

            // Initialize registers
            AddInstruction(Instruction.SetRegisterToImmediateValue(SpecialRegisters.StackPointer, initialStackPointer));
            AddInstructions(Enumerable.Range(3, RegisterCount - 2).Select(register => Instruction.SetRegisterToImmediateValue(register, 0)));

            // Clear the first 32 words of RAM
            AddInstructions(Enumerable.Range(RamAddress, 32).Select(address => Instruction.WriteMemory(addressValue: address, immediateValue: 0)));

            // Initialize heap
            AddInstruction(Instruction.WriteMemory(addressValue: HeapAddress, immediateValue: HeapAddress + 1));

            InitializeTypes();
            AddCallOrInlinedMethod(main);

            // Jump back to the beginning
            AddInstruction(Instruction.Jump(-(Instructions.Count + 1)));

            foreach (var method in nonInlinedMethods)
            {
                methodContexts[method] = AddMethod(method);
            }

            foreach (var (instruction, method) in calls)
            {
                if (methodContexts.TryGetValue(method, out var methodContext) && methodContext != null)
                {
                    instruction.SetJumpTarget(methodContext.InstructionIndex);
                }
                else
                {
                    Errors.Add($"Cannot call {method.DeclaringType.Name}.{method.Name} because it is not defined");
                }
            }

            if (Errors.Count > 0)
            {
                instructionsWriter.WriteLine("Errors:");
                foreach (var error in Errors)
                {
                    instructionsWriter.WriteLine(error);
                }
                instructionsWriter.WriteLine();
            }

            instructionsWriter.WriteLine("Types:");
            foreach (var type in types)
            {
                var typeInfo = GetTypeInfo(type);
                instructionsWriter.WriteLine($"{type.FullName}: reference={typeInfo.RuntimeTypeReference} size={typeInfo.Size} initialize={typeInfo.Initialize}" +
                    $" fields=({string.Join(", ", typeInfo.Fields.Select(field => $"{field.Key.Name}: offset={field.Value.Offset} size={field.Value.Size} type={field.Key.FieldType.FullName}"))})");
            }
            instructionsWriter.WriteLine();

            instructionsWriter.WriteLine("Method addresses:");
            foreach (var entry in methodRanges)
            {
                var method = entry.Key;
                instructionsWriter.WriteLine($"{method.DeclaringType.Name}.{method.Name}: {string.Join(", ", entry.Value.Select(range => $"{range.Item1 + 1}-{range.Item2 + 1}"))}");
            }
            instructionsWriter.WriteLine();

            if (staticFields.Count > 0)
            {
                instructionsWriter.WriteLine("Static field addresses:");
                foreach (var entry in staticFields.Where(entry => entry.Value.Size > 0))
                {
                    instructionsWriter.WriteLine($"{entry.Key.DeclaringType.Name}.{entry.Key.Name}: {entry.Value.Offset}");
                }
                instructionsWriter.WriteLine();
            }
        }

        private void AllocateStaticFields()
        {
            var currentAddress = RamAddress;

            foreach (var type in types)
            {
                var typeInfo = GetTypeInfo(type);

                if (typeInfo.Initialize)
                {
                    foreach (var field in type
                        .GetFields(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(field => !field.IsLiteral)) // Exclude constants
                    {
                        var size = GetVariableSize(field.FieldType);
                        staticFields[field] = new VariableInfo { Offset = currentAddress, Size = size };
                        currentAddress += size;
                    }
                }
            }

            initialStackPointer = currentAddress;
        }

        private (Dictionary<int, VariableInfo>, int) AllocateLocalVariables(IEnumerable<LocalVariableInfo> localVariables)
        {
            var variables = new Dictionary<int, VariableInfo>();
            var currentOffset = 0;

            if (localVariables != null)
            {
                foreach (var localVariable in localVariables)
                {
                    var size = GetVariableSize(localVariable.LocalType);
                    variables[localVariable.LocalIndex] = new VariableInfo { Offset = currentOffset, Size = size };
                    currentOffset += size;
                }
            }

            return (variables, currentOffset);
        }

        private MethodParameterInfo GetMethodParameterInfo(MethodBase method, ICollection<int> inlinedParameters = null)
        {
            if (!methodParameterInfoCache.TryGetValue(method, out var methodParameterInfo))
            {
                var parameters = new Dictionary<int, VariableInfo>();
                var positionOffset = 0;
                var currentOffset = 0;

                if (!method.IsStatic)
                {
                    parameters[positionOffset++] = new VariableInfo { Offset = currentOffset++, Size = 1 };
                }

                foreach (var parameter in method.GetParameters())
                {
                    var isInline = inlinedParameters?.Contains(parameter.Position) ?? false;
                    var size = !isInline ? GetVariableSize(parameter.ParameterType) : 0;
                    parameters[parameter.Position + positionOffset] = new VariableInfo { Offset = currentOffset, Size = size };
                    currentOffset += size;
                }

                methodParameterInfo = new MethodParameterInfo { Parameters = parameters, Size = currentOffset };
            }

            return methodParameterInfo;
        }

        private TypeInfo GetTypeInfo(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (!typeInfoCache.TryGetValue(type, out var typeInfo))
            {
                var fields = new Dictionary<FieldInfo, VariableInfo>();
                var currentOffset = 0;

                if (!type.IsValueType)
                {
                    var baseTypeInfo = GetTypeInfo(type.BaseType);

                    if (baseTypeInfo != null)
                    {
                        foreach (var entry in baseTypeInfo.Fields)
                        {
                            fields.Add(entry.Key, entry.Value);
                        }

                        currentOffset += baseTypeInfo.Size;
                    }
                    else
                    {
                        currentOffset++; // Leave room for the type reference
                    }
                }

                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .OrderBy(field => field.Name))
                {
                    var size = GetVariableSize(field.FieldType);
                    fields[field] = new VariableInfo { Offset = currentOffset, Size = size };
                    currentOffset += size;
                }

                typeInfo = new TypeInfo { Fields = fields, Size = currentOffset };
                typeInfoCache[type] = typeInfo;
            }

            return typeInfo;
        }

        private int GetVariableSize(Type type)
        {
            if (type.IsValueType && !type.IsPrimitive)
            {
                return GetTypeInfo(type).Size;
            }
            else if (type == typeof(long) || type == typeof(ulong) || type == typeof(double))
            {
                return 2;
            }
            else
            {
                return 1;
            }
        }

        private void EmitTypes()
        {
            const int runtimeTypeSize = 2;

            // Create type placeholders
            foreach (var type in types)
            {
                var typeInfo = GetTypeInfo(type);
                typeInfo.RuntimeTypeReference = AddData(Enumerable.Repeat(0, runtimeTypeSize));
            }

            var typeTypeReference = GetTypeInfo(typeof(Type)).RuntimeTypeReference;

            // Fill in types
            foreach (var type in types)
            {
                var typeInfo = GetTypeInfo(type);

                SetData(typeInfo.RuntimeTypeReference, 0, typeTypeReference);
                SetData(typeInfo.RuntimeTypeReference, 1, GetTypeInfo(type.BaseType)?.RuntimeTypeReference ?? 0);
            }
        }

        private void InitializeTypes()
        {
            foreach (var type in types)
            {
                var staticConstructor = GetTypeInfo(type).StaticConstructor;

                if (staticConstructor != null)
                {
                    AddMethod(staticConstructor, isInline: true);
                }
            }
        }

        private MethodContext AddMethod(MethodBase method, bool isInline = false, Dictionary<int, int> inlinedParameterValues = null)
        {
            // Stack frame layout:
            // Parameters
            // Return address (if inlined)
            // Local variables (stack pointer offset is relative to first local variable)
            // Evaluation stack

            var previousMethodContext = methodContext;
            var analysis = methodAnalyses[method];
            var ilInstructions = analysis.ILInstructions;
            var methodParameterInfo = GetMethodParameterInfo(method, inlinedParameterValues?.Keys);
            var (localVariables, localVariablesSize) = AllocateLocalVariables(!analysis.IsCompilerGenerated ? method.GetMethodBody()?.LocalVariables : null);
            var stackPointerOffsetMap = new Dictionary<ILInstruction, int>();
            var instructionOffsetToIndexMap = new Dictionary<int, int>();
            var jumps = new List<(Instruction, int)>();

            methodContext = new MethodContext
            {
                Method = method,
                InstructionIndex = Instructions.Count,
                IsInline = isInline,
                IsVoid = method.IsVoid(),
                Parameters = methodParameterInfo.Parameters,
                ParametersSize = methodParameterInfo.Size,
                LocalVariables = localVariables,
                LocalVariablesSize = localVariablesSize,
                Analysis = analysis,
                InlinedParameterValues = inlinedParameterValues ?? new Dictionary<int, int>()
            };

            var inlinedMethodCalls = analysis.MethodCalls
                .Where(callInstruction => !nonInlinedMethods.Contains((MethodBase)callInstruction.Operand))
                .ToDictionary(callInstruction => callInstruction, callInstruction =>
                {
                    var calledMethodAnalysis = methodAnalyses[(MethodBase)callInstruction.Operand];
                    var inlinedParameters = methodContext.Analysis.SourceSinkMap
                        .Where(entry => entry.Value.Instruction == callInstruction && !calledMethodAnalysis.NonInlineableParameters.Contains(entry.Value.Parameter))
                        .Select(entry => new { entry.Value.Parameter, Source = entry.Key, Value = GetConstantValueForInlinedMethod(entry.Key) })
                        .Where(entry => entry.Value.HasValue);

                    return new
                    {
                        InlinedParameterValues = inlinedParameters.ToDictionary(entry => entry.Parameter, entry => entry.Value.Value),
                        Sources = inlinedParameters.Select(entry => entry.Source)
                    };
                });

            var inlinedParameterSources = inlinedMethodCalls.Values.SelectMany(inlinedMethodCall => inlinedMethodCall.Sources).ToHashSet();

            try
            {
                AdjustStackPointer(localVariablesSize);

                if (analysis.IsCompilerGenerated)
                {
                    AddCompilerGeneratedMethod(method);
                }

                var ilInstructionIndex = 0;
                foreach (var ilInstruction in ilInstructions)
                {
                    instructionOffsetToIndexMap[ilInstruction.Offset] = Instructions.Count;

                    var opCodeValue = ilInstruction.Code.Value;
                    var isInlinedParameterSource = inlinedParameterSources.Contains(ilInstruction);

                    if (analysis.DiscontinuityMap.TryGetValue(ilInstruction, out var jumpSource) &&
                        stackPointerOffsetMap.TryGetValue(jumpSource, out var stackPointerOffset))
                    {
                        methodContext.StackPointerOffset = stackPointerOffset;
                    }

                    // Opcode reference: https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes?view=netcore-3.1

                    if (opCodeValue == OpCodes.Pop.Value)
                    {
                        AdjustStackPointer(-1);
                    }
                    else if (opCodeValue == OpCodes.Dup.Value)
                    {
                        AddInstruction(Instruction.ReadStackValue(-1, 3));
                        AddInstructions(Instruction.NoOp(4));
                        AddInstruction(Instruction.PushRegister(3));
                    }
                    else if (IsLoadSingleWordConstant(opCodeValue))
                    {
                        if (!isInlinedParameterSource)
                        {
                            AddInstruction(Instruction.PushImmediateValue(GetConstantValue(ilInstruction)));
                        }
                    }
                    else if (opCodeValue == OpCodes.Ldc_I8.Value ||
                        opCodeValue == OpCodes.Ldc_R8.Value)
                    {
                        ulong operand = (ulong)ilInstruction.Operand;

                        AddInstruction(Instruction.PushImmediateValue((int)operand));
                        AddInstruction(Instruction.PushImmediateValue((int)(operand >> 32)));
                    }
                    else if (IsLoadArgument(opCodeValue))
                    {
                        if (!isInlinedParameterSource)
                        {
                            PushArgument(GetLoadArgumentPosition(ilInstruction));
                        }
                    }
                    else if (opCodeValue == OpCodes.Starg_S.Value)
                    {
                        PopArgument((byte)ilInstruction.Operand);
                    }
                    else if (opCodeValue == OpCodes.Ldarga_S.Value)
                    {
                        var operand = (byte)ilInstruction.Operand;

                        AddInstruction(Instruction.Push(inputRegister: SpecialRegisters.StackPointer, immediateValue: methodContext.Parameters[operand].Offset - methodContext.ParametersSize - (methodContext.IsInline ? 0 : 1) - methodContext.StackPointerOffset));
                    }
                    else if (IsLoadLocal(opCodeValue))
                    {
                        PushLocalVariable(GetLoadLocalIndex(ilInstruction));
                    }
                    else if (IsStoreLocal(opCodeValue))
                    {
                        PopLocalVariable(GetStoreLocalIndex(ilInstruction));
                    }
                    else if (opCodeValue == OpCodes.Ldloca_S.Value)
                    {
                        var operand = (byte)ilInstruction.Operand;

                        AddInstruction(Instruction.Push(inputRegister: SpecialRegisters.StackPointer, immediateValue: localVariables[operand].Offset - methodContext.StackPointerOffset));
                    }
                    else if (opCodeValue == OpCodes.Ldind_I.Value ||
                        opCodeValue == OpCodes.Ldind_I1.Value ||
                        opCodeValue == OpCodes.Ldind_U1.Value ||
                        opCodeValue == OpCodes.Ldind_I2.Value ||
                        opCodeValue == OpCodes.Ldind_U2.Value ||
                        opCodeValue == OpCodes.Ldind_I4.Value ||
                        opCodeValue == OpCodes.Ldind_U4.Value ||
                        opCodeValue == OpCodes.Ldind_R4.Value ||
                        opCodeValue == OpCodes.Ldind_Ref.Value)
                    {
                        PushMemory();
                    }
                    else if (opCodeValue == OpCodes.Stind_I.Value ||
                        opCodeValue == OpCodes.Stind_I1.Value ||
                        opCodeValue == OpCodes.Stind_I2.Value ||
                        opCodeValue == OpCodes.Stind_I4.Value ||
                        opCodeValue == OpCodes.Stind_R4.Value ||
                        opCodeValue == OpCodes.Stind_Ref.Value)
                    {
                        PopMemory();
                    }
                    else if (opCodeValue == OpCodes.Ldind_I8.Value ||
                        opCodeValue == OpCodes.Ldind_R8.Value)
                    {
                        PushMemory(size: 2);
                    }
                    else if (opCodeValue == OpCodes.Stind_I8.Value ||
                        opCodeValue == OpCodes.Stind_R8.Value)
                    {
                        PopMemory(size: 2);
                    }
                    else if (opCodeValue == OpCodes.Ldobj.Value)
                    {
                        var operand = (Type)ilInstruction.Operand;
                        var size = GetVariableSize(operand);

                        PushMemory(size: size);
                    }
                    else if (opCodeValue == OpCodes.Stobj.Value)
                    {
                        var operand = (Type)ilInstruction.Operand;
                        var size = GetVariableSize(operand);

                        PopMemory(size: size);
                    }
                    else if (opCodeValue == OpCodes.Ldsfld.Value)
                    {
                        var operand = (FieldInfo)ilInstruction.Operand;

                        if (staticFields.TryGetValue(operand, out var field))
                        {
                            CopyData(field.Size,
                                (offset, index) => Instruction.ReadMemory(3 + index, addressValue: field.Offset + offset),
                                (offset, index) => Instruction.PushRegister(3 + index));
                        }
                        else
                        {
                            throw new Exception($"Field {operand.DeclaringType}.{operand.Name} not allocated");
                        }
                    }
                    else if (opCodeValue == OpCodes.Stsfld.Value)
                    {
                        var operand = (FieldInfo)ilInstruction.Operand;

                        if (staticFields.TryGetValue(operand, out var field))
                        {
                            // Start at the end of the variable since popping happens from right to left
                            var baseOffset = field.Offset + field.Size - 1;

                            CopyData(field.Size,
                                (offset, index) => Instruction.Pop(3 + index),
                                (offset, index) => Instruction.WriteMemory(addressValue: baseOffset - offset, inputRegister: 3 + index));
                        }
                        else
                        {
                            throw new Exception($"Field {operand.DeclaringType}.{operand.Name} not allocated");
                        }
                    }
                    else if (opCodeValue == OpCodes.Ldsflda.Value)
                    {
                        var operand = (FieldInfo)ilInstruction.Operand;

                        if (staticFields.TryGetValue(operand, out var field))
                        {
                            AddInstruction(Instruction.PushImmediateValue(field.Offset));
                        }
                        else
                        {
                            throw new Exception($"Field {operand.DeclaringType}.{operand.Name} not allocated");
                        }
                    }
                    else if (opCodeValue == OpCodes.Ldfld.Value)
                    {
                        var operand = (FieldInfo)ilInstruction.Operand;
                        var typeInfo = GetTypeInfo(operand.DeclaringType);
                        var field = typeInfo.Fields[operand];

                        if (operand.DeclaringType.IsValueType)
                        {
                            if (field.Size == 1)
                            {
                                AddInstruction(Instruction.ReadStackValue(field.Offset - typeInfo.Size, 3, stackPointerAdjustment: -typeInfo.Size));
                                AddInstructions(Instruction.NoOp(4));
                                AddInstruction(Instruction.PushRegister(3));
                            }
                            else
                            {
                                AddInstruction(Instruction.AdjustStackPointer(-typeInfo.Size));
                                PushVariable(field, offsetRelativeToBase: methodContext.StackPointerOffset);
                            }
                        }
                        else
                        {
                            AddInstruction(Instruction.Pop(3)); // Pointer to beginning of object
                            AddInstructions(Instruction.NoOp(4));

                            CopyData(field.Size,
                                (offset, index) => Instruction.ReadMemory(outputRegister: 4 + index, addressRegister: 3, addressValue: field.Offset + offset),
                                (offset, index) => Instruction.PushRegister(4 + index));
                        }
                    }
                    else if (opCodeValue == OpCodes.Stfld.Value)
                    {
                        var operand = (FieldInfo)ilInstruction.Operand;
                        var typeInfo = GetTypeInfo(operand.DeclaringType);
                        var field = typeInfo.Fields[operand];

                        if (field.Size == 1)
                        {
                            AddInstruction(Instruction.Pop(4)); // Value
                            AddInstruction(Instruction.Pop(3)); // Pointer to beginning of object
                            AddInstructions(Instruction.NoOp(4));
                            AddInstruction(Instruction.WriteMemory(addressRegister: 3, addressValue: field.Offset, inputRegister: 4));
                        }
                        else
                        {
                            AddInstruction(Instruction.ReadStackValue(-field.Size - 1, 3)); // Pointer to beginning of object

                            // Start at the end of the variable since popping happens from right to left
                            var baseOffset = field.Offset + field.Size - 1;

                            CopyData(field.Size,
                                (offset, index) => Instruction.Pop(4 + index),
                                (offset, index) => Instruction.WriteMemory(addressRegister: 3, addressValue: baseOffset - offset, inputRegister: 4 + index));

                            AddInstruction(Instruction.AdjustStackPointer(-1)); // Pop off the object reference
                        }
                    }
                    else if (opCodeValue == OpCodes.Ldflda.Value)
                    {
                        var operand = (FieldInfo)ilInstruction.Operand;
                        var typeInfo = GetTypeInfo(operand.DeclaringType);

                        AddInstruction(Instruction.Pop(3)); // Pointer to beginning of structure
                        AddInstructions(Instruction.NoOp(4));
                        AddInstruction(Instruction.Push(inputRegister: 3, immediateValue: typeInfo.Fields[operand].Offset));
                    }
                    else if (opCodeValue == OpCodes.Ldelem.Value)
                    {
                        var operand = (Type)ilInstruction.Operand;
                        var size = GetVariableSize(operand);

                        PushArrayElement(size);
                    }
                    else if (opCodeValue == OpCodes.Ldelem_I.Value ||
                        opCodeValue == OpCodes.Ldelem_I1.Value ||
                        opCodeValue == OpCodes.Ldelem_U1.Value ||
                        opCodeValue == OpCodes.Ldelem_I2.Value ||
                        opCodeValue == OpCodes.Ldelem_U2.Value ||
                        opCodeValue == OpCodes.Ldelem_I4.Value ||
                        opCodeValue == OpCodes.Ldelem_U4.Value ||
                        opCodeValue == OpCodes.Ldelem_Ref.Value)
                    {
                        PushArrayElement(1);
                    }
                    else if (opCodeValue == OpCodes.Ldelem_I8.Value ||
                        opCodeValue == OpCodes.Ldelem_R8.Value)
                    {
                        PushArrayElement(2);
                    }
                    else if (opCodeValue == OpCodes.Stelem.Value)
                    {
                        var operand = (Type)ilInstruction.Operand;
                        var size = GetVariableSize(operand);

                        PopArrayElement(size);
                    }
                    else if (opCodeValue == OpCodes.Stelem_I.Value ||
                        opCodeValue == OpCodes.Stelem_I1.Value ||
                        opCodeValue == OpCodes.Stelem_I2.Value ||
                        opCodeValue == OpCodes.Stelem_I4.Value ||
                        opCodeValue == OpCodes.Stelem_Ref.Value)
                    {
                        PopArrayElement(1);
                    }
                    else if (opCodeValue == OpCodes.Stelem_I8.Value ||
                        opCodeValue == OpCodes.Stelem_R8.Value)
                    {
                        PopArrayElement(2);
                    }
                    else if (opCodeValue == OpCodes.Ldelema.Value)
                    {
                        var operand = (Type)ilInstruction.Operand;
                        var size = GetVariableSize(operand);

                        PushArrayElementAddress(size);
                    }
                    else if (opCodeValue == OpCodes.Initobj.Value)
                    {
                        var operand = (Type)ilInstruction.Operand;
                        var typeInfo = GetTypeInfo(operand);

                        AddInstruction(Instruction.Pop(3));
                        AddInstructions(Instruction.NoOp(4));

                        // Clear fields
                        for (int index = 0; index < typeInfo.Size; index++)
                        {
                            AddInstruction(Instruction.WriteMemory(addressRegister: 3, addressValue: index, immediateValue: 0));
                        }
                    }
                    else if (opCodeValue == OpCodes.Ldftn.Value)
                    {
                        var operand = (MethodInfo)ilInstruction.Operand;

                        // TODO: implement
                    }
                    else if (opCodeValue == OpCodes.Box.Value)
                    {
                        var operand = (Type)ilInstruction.Operand;
                        var typeInfo = GetTypeInfo(operand);
                        var size = typeInfo.Size;

                        AddInstruction(Instruction.ReadMemory(3, addressValue: HeapAddress));

                        CopyData(size,
                            (offset, index) => Instruction.Pop(4 + index),
                            (offset, index) => Instruction.WriteMemory(addressRegister: 3, addressValue: size - offset, inputRegister: 4 + index));

                        AddInstruction(Instruction.WriteMemory(addressValue: HeapAddress, inputRegister: 3, immediateValue: size + 1)); // Allocate the box object on the heap
                        AddInstruction(Instruction.WriteMemory(addressRegister: 3, immediateValue: typeInfo.RuntimeTypeReference)); // Store the type reference
                        AddInstruction(Instruction.PushRegister(3)); // Push the box reference onto the stack
                    }
                    else if (opCodeValue == OpCodes.Unbox_Any.Value)
                    {
                        var operand = (Type)ilInstruction.Operand;
                        var size = GetVariableSize(operand);

                        AddInstruction(Instruction.Pop(3)); // Box reference
                        AddInstructions(Instruction.NoOp(4));

                        CopyData(size,
                            (offset, index) => Instruction.ReadMemory(outputRegister: 4 + index, addressRegister: 3, addressValue: offset + 1),
                            (offset, index) => Instruction.PushRegister(4 + index));
                    }
                    else if (opCodeValue == OpCodes.Sizeof.Value)
                    {
                        var operand = (Type)ilInstruction.Operand;
                        var size = GetVariableSize(operand);

                        AddInstruction(Instruction.PushImmediateValue(size));
                    }
                    else if (opCodeValue == OpCodes.Newobj.Value)
                    {
                        var operand = (ConstructorInfo)ilInstruction.Operand;
                        var constructorParameterInfo = GetMethodParameterInfo(operand);
                        var typeInfo = GetTypeInfo(operand.DeclaringType);

                        AddInstruction(Instruction.ReadMemory(3, addressValue: HeapAddress));
                        AddInstructions(Instruction.NoOp(4));
                        AddInstruction(Instruction.WriteMemory(addressValue: HeapAddress, inputRegister: 3, immediateValue: typeInfo.Size)); // Allocate the object on the heap
                        AddInstruction(Instruction.WriteMemory(addressRegister: 3, immediateValue: typeInfo.RuntimeTypeReference)); // Store the type reference
                        AddInstruction(Instruction.PushRegister(3)); // Push the object reference onto the stack

                        // Clear fields
                        for (int offset = 1; offset < typeInfo.Size; offset++)
                        {
                            AddInstruction(Instruction.WriteMemory(addressRegister: 3, addressValue: offset, immediateValue: 0));
                        }

                        // Push constructor parameters
                        AddInstruction(Instruction.PushRegister(3)); // The first parameter is the object reference

                        var baseOffset = methodContext.StackPointerOffset - constructorParameterInfo.Size - 1;

                        CopyData(constructorParameterInfo.Size - 1,
                            (offset, index) => Instruction.ReadStackValue(baseOffset - methodContext.StackPointerOffset + offset, outputRegister: 4 + index),
                            (offset, index) => Instruction.PushRegister(4 + index));

                        AddCallOrInlinedMethod(operand);

                        // Remove the constructor parameters from the stack, leaving the object reference on top
                        if (constructorParameterInfo.Size > 1)
                        {
                            AddInstruction(Instruction.Pop(3, additionalStackPointerAdjustment: constructorParameterInfo.Size - 1));
                            AddInstructions(Instruction.NoOp(4));
                            AddInstruction(Instruction.PushRegister(3));
                        }
                    }
                    else if (opCodeValue == OpCodes.Newarr.Value)
                    {
                        var operand = (Type)ilInstruction.Operand;
                        var size = GetVariableSize(operand);
                        var arrayTypeInfo = GetTypeInfo(operand.MakeArrayType());

                        AddInstruction(Instruction.Pop(3)); // Array length
                        AddInstruction(Instruction.ReadMemory(4, addressValue: HeapAddress));

                        // Calculate the total size of the array
                        int arraySizeRegister;
                        if (size != 1)
                        {
                            arraySizeRegister = 6;
                            AddInstructions(Instruction.NoOp(3));
                            AddInstruction(Instruction.BinaryOperation(Operation.Multiply, outputRegister: arraySizeRegister, leftInputRegister: 3, rightImmediateValue: size));
                        }
                        else
                        {
                            arraySizeRegister = 3;
                        }

                        AddInstructions(Instruction.NoOp(4));
                        AddInstruction(Instruction.BinaryOperation(Operation.Add, outputRegister: 5, leftInputRegister: 4, rightInputRegister: arraySizeRegister, rightImmediateValue: 2)); // Calculate the new free pointer

                        if (arraySizeRegister != 6)
                        {
                            AddInstruction(Instruction.SetRegister(6, inputRegister: arraySizeRegister)); // Initialize the (decrementing) counter for clearing the array
                        }

                        AddInstruction(Instruction.PushRegister(4)); // Push the array reference onto the stack
                        AddInstruction(Instruction.WriteMemory(addressRegister: 4, immediateValue: arrayTypeInfo.RuntimeTypeReference, autoIncrement: 1)); // Store the type reference
                        AddInstruction(Instruction.WriteMemory(addressRegister: 4, inputRegister: 3, autoIncrement: 1)); // Write the array length to the beginning of the array

                        // Loop to clear array
                        AddInstruction(Instruction.WriteMemory(addressRegister: 4, autoIncrement: 1)); // Leave room for the type reference and array length
                        AddInstruction(Instruction.IncrementRegister(6, -1));
                        AddInstruction(Instruction.JumpIf(-3, conditionLeftRegister: 6, conditionOperator: ConditionOperator.GreaterThan));

                        AddInstruction(Instruction.WriteMemory(addressValue: HeapAddress, inputRegister: 5)); // Allocate the array on the heap
                    }
                    else if (opCodeValue == OpCodes.Ldlen.Value)
                    {
                        PushMemory(addressOffset: 1);
                    }
                    else if (opCodeValue == OpCodes.Ldtoken.Value)
                    {
                        if (ilInstruction.Operand is FieldInfo)
                        {
                            var operand = (FieldInfo)ilInstruction.Operand;

                            var arraySizeInstruction = ilInstructions[ilInstructionIndex - 3];
                            var newArrayInstruction = ilInstructions[ilInstructionIndex - 2];

                            int arraySize = GetConstantValue(arraySizeInstruction);
                            var array = Array.CreateInstance((Type)newArrayInstruction.Operand, arraySize);
                            RuntimeHelpers.InitializeArray(array, operand.FieldHandle);

                            PushConstantArray(array);
                        }
                        else if (ilInstruction.Operand is Type)
                        {
                            // TODO: implement
                        }
                        else
                        {
                            throw new NotImplementedException($"Loading tokens of type {ilInstruction.Operand.GetType()} is not currently supported");
                        }
                    }
                    else if (opCodeValue == OpCodes.Ldstr.Value)
                    {
                        var operand = (string)ilInstruction.Operand;
                        var typeInfo = GetTypeInfo(typeof(string));

                        PushConstantArray(operand.ToCharArray(), typeInfo.RuntimeTypeReference);
                    }
                    else if (opCodeValue == OpCodes.Isinst.Value)
                    {
                        var operand = (Type)ilInstruction.Operand;
                        var typeInfo = GetTypeInfo(operand);

                        AddInstruction(Instruction.Pop(3)); // Reference to check
                        AddInstructions(Instruction.NoOp(4));
                        AddInstruction(Instruction.ReadMemory(outputRegister: 4, addressRegister: 3));
                        AddInstructions(Instruction.NoOp(3));
                        AddInstruction(Instruction.SetRegisterToImmediateValue(5, 0));
                        AddInstruction(Instruction.SetRegister(5, inputRegister: 3, conditionLeftRegister: 4, conditionRightImmediateValue: typeInfo.RuntimeTypeReference, conditionOperator: ConditionOperator.IsEqual));

                        // TODO: check against parent classes

                        AddInstructions(Instruction.NoOp(4));
                        AddInstruction(Instruction.PushRegister(5));
                    }
                    else if (opCodeValue == OpCodes.Not.Value)
                    {
                        AddInstruction(Instruction.Pop(3));
                        AddInstructions(Instruction.NoOp(4));
                        AddInstruction(Instruction.BinaryOperation(Operation.Xor, outputRegister: 4, leftInputRegister: 3, rightImmediateValue: -1));
                        AddInstructions(Instruction.NoOp(4));
                        AddInstruction(Instruction.Push(4));
                    }
                    else if (opCodeValue == OpCodes.Neg.Value)
                    {
                        AddInstruction(Instruction.Pop(3));
                        AddInstructions(Instruction.NoOp(4));
                        AddInstruction(Instruction.BinaryOperation(Operation.Subtract, outputRegister: 4, rightInputRegister: 3));
                        AddInstructions(Instruction.NoOp(4));
                        AddInstruction(Instruction.Push(4));
                    }
                    else if (opCodeValue == OpCodes.Mul.Value ||
                        opCodeValue == OpCodes.Mul_Ovf.Value ||
                        opCodeValue == OpCodes.Mul_Ovf_Un.Value)
                    {
                        AddBinaryOperation(Operation.Multiply);
                    }
                    else if (opCodeValue == OpCodes.Div.Value ||
                        opCodeValue == OpCodes.Div_Un.Value)
                    {
                        AddBinaryOperation(Operation.Divide);
                    }
                    else if (opCodeValue == OpCodes.Add.Value ||
                        opCodeValue == OpCodes.Add_Ovf.Value ||
                        opCodeValue == OpCodes.Add_Ovf_Un.Value)
                    {
                        AddBinaryOperation(Operation.Add);
                    }
                    else if (opCodeValue == OpCodes.Sub.Value)
                    {
                        AddBinaryOperation(Operation.Subtract);
                    }
                    else if (opCodeValue == OpCodes.Rem.Value ||
                        opCodeValue == OpCodes.Rem_Un.Value)
                    {
                        AddBinaryOperation(Operation.Mod);
                    }
                    else if (opCodeValue == OpCodes.Shl.Value)
                    {
                        AddBinaryOperation(Operation.LeftShift);
                    }
                    else if (opCodeValue == OpCodes.Shr.Value ||
                        opCodeValue == OpCodes.Shr_Un.Value)
                    {
                        AddBinaryOperation(Operation.RightShift);
                    }
                    else if (opCodeValue == OpCodes.And.Value)
                    {
                        AddBinaryOperation(Operation.And);
                    }
                    else if (opCodeValue == OpCodes.Or.Value)
                    {
                        AddBinaryOperation(Operation.Or);
                    }
                    else if (opCodeValue == OpCodes.Xor.Value)
                    {
                        AddBinaryOperation(Operation.Xor);
                    }
                    else if (opCodeValue == OpCodes.Ceq.Value)
                    {
                        AddComparison(ConditionOperator.IsEqual);
                    }
                    else if (opCodeValue == OpCodes.Cgt.Value)
                    {
                        AddComparison(ConditionOperator.GreaterThan);
                    }
                    else if (opCodeValue == OpCodes.Cgt_Un.Value)
                    {
                        AddComparison(ConditionOperator.GreaterThan, unsigned: true);
                    }
                    else if (opCodeValue == OpCodes.Clt.Value)
                    {
                        AddComparison(ConditionOperator.LessThan);
                    }
                    else if (opCodeValue == OpCodes.Clt_Un.Value)
                    {
                        AddComparison(ConditionOperator.LessThan, unsigned: true);
                    }
                    else if (opCodeValue == OpCodes.Br.Value ||
                        opCodeValue == OpCodes.Br_S.Value ||
                        opCodeValue == OpCodes.Leave.Value ||
                        opCodeValue == OpCodes.Leave_S.Value)
                    {
                        var operand = (int)ilInstruction.Operand;

                        if (opCodeValue == OpCodes.Leave.Value || opCodeValue == OpCodes.Leave_S.Value)
                        {
                            AdjustStackPointer(localVariablesSize - methodContext.StackPointerOffset); // Clear any operands off the stack
                        }

                        if (operand != ilInstructions.ElementAtOrDefault(ilInstructionIndex + 1)?.Offset)
                        {
                            jumps.Add((AddInstruction(Instruction.Jump(-(Instructions.Count + 1))), operand));
                        }
                    }
                    else if (opCodeValue == OpCodes.Brtrue.Value ||
                        opCodeValue == OpCodes.Brtrue_S.Value)
                    {
                        var operand = (int)ilInstruction.Operand;

                        AddJumpIf(operand, ConditionOperator.IsNotEqual, jumps);
                    }
                    else if (opCodeValue == OpCodes.Brfalse.Value ||
                        opCodeValue == OpCodes.Brfalse_S.Value)
                    {
                        var operand = (int)ilInstruction.Operand;

                        AddJumpIf(operand, ConditionOperator.IsEqual, jumps);
                    }
                    else if (opCodeValue == OpCodes.Beq.Value ||
                        opCodeValue == OpCodes.Beq_S.Value)
                    {
                        var operand = (int)ilInstruction.Operand;

                        AddBinaryJumpIf(operand, ConditionOperator.IsEqual, jumps);
                    }
                    else if (opCodeValue == OpCodes.Bne_Un.Value ||
                        opCodeValue == OpCodes.Bne_Un_S.Value)
                    {
                        var operand = (int)ilInstruction.Operand;

                        AddBinaryJumpIf(operand, ConditionOperator.IsNotEqual, jumps);
                    }
                    else if (opCodeValue == OpCodes.Bgt.Value ||
                        opCodeValue == OpCodes.Bgt_S.Value)
                    {
                        var operand = (int)ilInstruction.Operand;

                        AddBinaryJumpIf(operand, ConditionOperator.GreaterThan, jumps);
                    }
                    else if (opCodeValue == OpCodes.Bgt_Un.Value ||
                        opCodeValue == OpCodes.Bgt_Un_S.Value)
                    {
                        var operand = (int)ilInstruction.Operand;

                        AddBinaryJumpIf(operand, ConditionOperator.GreaterThan, jumps, unsigned: true);
                    }
                    else if (opCodeValue == OpCodes.Blt.Value ||
                        opCodeValue == OpCodes.Blt_S.Value)
                    {
                        var operand = (int)ilInstruction.Operand;

                        AddBinaryJumpIf(operand, ConditionOperator.LessThan, jumps);
                    }
                    else if (opCodeValue == OpCodes.Blt_Un.Value ||
                        opCodeValue == OpCodes.Blt_Un_S.Value)
                    {
                        var operand = (int)ilInstruction.Operand;

                        AddBinaryJumpIf(operand, ConditionOperator.LessThan, jumps, unsigned: true);
                    }
                    else if (opCodeValue == OpCodes.Bge.Value ||
                        opCodeValue == OpCodes.Bge_S.Value)
                    {
                        var operand = (int)ilInstruction.Operand;

                        AddBinaryJumpIf(operand, ConditionOperator.GreaterThanOrEqual, jumps);
                    }
                    else if (opCodeValue == OpCodes.Bge_Un.Value ||
                        opCodeValue == OpCodes.Bge_Un_S.Value)
                    {
                        var operand = (int)ilInstruction.Operand;

                        AddBinaryJumpIf(operand, ConditionOperator.GreaterThanOrEqual, jumps, unsigned: true);
                    }
                    else if (opCodeValue == OpCodes.Ble.Value ||
                        opCodeValue == OpCodes.Ble_S.Value)
                    {
                        var operand = (int)ilInstruction.Operand;

                        AddBinaryJumpIf(operand, ConditionOperator.LessThanOrEqual, jumps);
                    }
                    else if (opCodeValue == OpCodes.Ble_Un.Value ||
                        opCodeValue == OpCodes.Ble_Un_S.Value)
                    {
                        var operand = (int)ilInstruction.Operand;

                        AddBinaryJumpIf(operand, ConditionOperator.LessThanOrEqual, jumps, unsigned: true);
                    }
                    else if (opCodeValue == OpCodes.Call.Value ||
                        opCodeValue == OpCodes.Callvirt.Value)
                    {
                        var operand = (MethodBase)ilInstruction.Operand;
                        var parameterValues = inlinedMethodCalls.TryGetValue(ilInstruction, out var inlinedMethodCall) ? inlinedMethodCall.InlinedParameterValues : null;

                        AddCallOrInlinedMethod(operand, inlinedParameterValues: parameterValues);
                    }
                    else if (opCodeValue == OpCodes.Ret.Value)
                    {
                        AddReturn();
                    }
                    else if (opCodeValue == OpCodes.Throw.Value)
                    {
                        // TODO: implement
                    }
                    else if (opCodeValue == OpCodes.Rethrow.Value)
                    {
                        // TODO: implement
                    }
                    else if (opCodeValue == OpCodes.Endfilter.Value)
                    {
                        // TODO: implement
                    }
                    else if (opCodeValue == OpCodes.Endfinally.Value)
                    {
                        // TODO: implement
                    }
                    else if (opCodeValue == OpCodes.Localloc.Value)
                    {
                        AddInstruction(Instruction.Pop(3)); // Size

                        // TODO: implement
                    }
                    else if (opCodeValue == OpCodes.Switch.Value)
                    {
                        // TODO: implement
                    }
                    else if (opCodeValue == OpCodes.Conv_I1.Value ||
                        opCodeValue == OpCodes.Conv_Ovf_I1.Value ||
                        opCodeValue == OpCodes.Conv_U1.Value ||
                        opCodeValue == OpCodes.Conv_Ovf_U1.Value ||
                        opCodeValue == OpCodes.Conv_I2.Value ||
                        opCodeValue == OpCodes.Conv_Ovf_I2.Value ||
                        opCodeValue == OpCodes.Conv_U2.Value ||
                        opCodeValue == OpCodes.Conv_Ovf_U2.Value ||
                        opCodeValue == OpCodes.Conv_I4.Value ||
                        opCodeValue == OpCodes.Conv_Ovf_I4.Value ||
                        opCodeValue == OpCodes.Conv_U4.Value ||
                        opCodeValue == OpCodes.Conv_Ovf_U4.Value ||
                        opCodeValue == OpCodes.Conv_I8.Value ||
                        opCodeValue == OpCodes.Conv_Ovf_I8.Value ||
                        opCodeValue == OpCodes.Conv_U8.Value ||
                        opCodeValue == OpCodes.Conv_Ovf_U8.Value ||
                        opCodeValue == OpCodes.Conv_I.Value ||
                        opCodeValue == OpCodes.Conv_Ovf_I.Value ||
                        opCodeValue == OpCodes.Conv_Ovf_I_Un.Value ||
                        opCodeValue == OpCodes.Conv_U.Value ||
                        opCodeValue == OpCodes.Conv_Ovf_U.Value ||
                        opCodeValue == OpCodes.Conv_Ovf_U_Un.Value ||
                        opCodeValue == OpCodes.Conv_R4.Value ||
                        opCodeValue == OpCodes.Conv_R8.Value ||
                        opCodeValue == OpCodes.Castclass.Value ||
                        opCodeValue == OpCodes.Constrained.Value ||
                        opCodeValue == OpCodes.Volatile.Value)
                    {
                        // TODO: implement
                    }
                    else if (opCodeValue != OpCodes.Nop.Value)
                    {
                        throw new NotImplementedException($"Unsupported opcode {ilInstruction.Code.Name}");
                    }

                    stackPointerOffsetMap[ilInstruction] = methodContext.StackPointerOffset;

                    ilInstructionIndex++;
                }

                foreach (var (instruction, offset) in jumps)
                {
                    if (instructionOffsetToIndexMap.TryGetValue(offset, out var jumpTarget))
                    {
                        instruction.SetJumpTarget(jumpTarget);
                    }
                    else
                    {
                        Errors.Add("Invalid jump target");
                    }
                }

                if (Instructions.Count > methodContext.InstructionIndex)
                {
                    if (!methodRanges.TryGetValue(method, out var methodRangeList))
                    {
                        methodRangeList = new List<(int, int)>();
                        methodRanges[method] = methodRangeList;
                    }

                    methodRangeList.Add((methodContext.InstructionIndex, Instructions.Count));
                }

                return methodContext;
            }
            finally
            {
                methodContext = previousMethodContext;
            }
        }

        private void PushConstantArray(Array array, int? typeReference = null)
        {
            var elementType = array.GetType().GetElementType();
            var size = GetVariableSize(elementType);

            IEnumerable<int> GetValues()
            {
                if (typeReference.HasValue)
                {
                    yield return typeReference.Value;
                }

                foreach (var value in array)
                {
                    if (value is float floatValue)
                    {
                        yield return BitConverter.SingleToInt32Bits(floatValue);
                    }
                    else if (value is double doubleValue)
                    {
                        var longValue = BitConverter.DoubleToInt64Bits(doubleValue);
                        yield return (int)(longValue & 0xFFFFFFFF);
                        yield return (int)(longValue >> 32);
                    }
                    else if (value is ulong ulongValue)
                    {
                        var longValue = (long)ulongValue;
                        yield return (int)(longValue & 0xFFFFFFFF);
                        yield return (int)(longValue >> 32);
                    }
                    else if (size == 1)
                    {
                        yield return Convert.ToInt32(value);
                    }
                    else if (size == 2)
                    {
                        var longValue = Convert.ToInt64(value);
                        yield return (int)(longValue & 0xFFFFFFFF);
                        yield return (int)(longValue >> 32);
                    }
                    else
                    {
                        throw new NotImplementedException($"Unsupported constant array element type: {value.GetType()}");
                    }
                }
            }

            AddInstruction(Instruction.PushImmediateValue(AddData(GetValues()))); // Push the array reference onto the stack
        }

        private void PushArrayElement(int size)
        {
            AddInstruction(Instruction.Pop(4)); // Index
            AddInstruction(Instruction.Pop(3)); // Array
            AddInstructions(Instruction.NoOp(4));

            // TODO: check bounds

            if (size != 1)
            {
                AddInstruction(Instruction.BinaryOperation(Operation.Multiply, outputRegister: 4, leftInputRegister: 4, rightImmediateValue: size));
                AddInstructions(Instruction.NoOp(4));
            }

            AddInstruction(Instruction.BinaryOperation(Operation.Add, outputRegister: 5, leftInputRegister: 3, rightInputRegister: 4, rightImmediateValue: 2)); // Calculate starting address
            AddInstructions(Instruction.NoOp(4));

            CopyData(size,
                (offset, index) => Instruction.ReadMemory(6 + index, addressRegister: 5, addressValue: offset),
                (offset, index) => Instruction.PushRegister(6 + index));
        }

        private void PopArrayElement(int size)
        {
            if (size == 1)
            {
                AddInstruction(Instruction.Pop(6)); // Value
                AddInstruction(Instruction.Pop(4)); // Index
                AddInstruction(Instruction.Pop(3)); // Array
                AddInstructions(Instruction.NoOp(4));

                // TODO: check bounds

                AddInstruction(Instruction.BinaryOperation(Operation.Add, outputRegister: 5, leftInputRegister: 3, rightInputRegister: 4, rightImmediateValue: 2)); // Calculate address
                AddInstructions(Instruction.NoOp(4));
                AddInstruction(Instruction.WriteMemory(addressRegister: 5, inputRegister: 6));
            }
            else
            {
                AddInstruction(Instruction.ReadStackValue(-size - 1, outputRegister: 4)); // Index
                AddInstruction(Instruction.ReadStackValue(-size - 2, outputRegister: 3)); // Array
                AddInstructions(Instruction.NoOp(4));

                // TODO: check bounds

                AddInstruction(Instruction.BinaryOperation(Operation.Multiply, outputRegister: 4, leftInputRegister: 4, rightImmediateValue: size));
                AddInstructions(Instruction.NoOp(4));
                AddInstruction(Instruction.BinaryOperation(Operation.Add, outputRegister: 5, leftInputRegister: 3, rightInputRegister: 4, rightImmediateValue: size + 1)); // Calculate starting address
                AddInstructions(Instruction.NoOp(4));

                CopyData(size,
                    (offset, index) => Instruction.Pop(6 + index),
                    (offset, index) => Instruction.WriteMemory(addressRegister: 5, addressValue: -offset, inputRegister: 6 + index));

                AddInstruction(Instruction.AdjustStackPointer(-2)); // Pop off the index and array
            }
        }

        private void PushArrayElementAddress(int size)
        {
            AddInstruction(Instruction.Pop(4)); // Index
            AddInstruction(Instruction.Pop(3)); // Array
            AddInstructions(Instruction.NoOp(4));

            // TODO: check bounds

            if (size != 1)
            {
                AddInstruction(Instruction.BinaryOperation(Operation.Multiply, outputRegister: 4, leftInputRegister: 4, rightImmediateValue: size));
                AddInstructions(Instruction.NoOp(4));
            }

            AddInstruction(Instruction.BinaryOperation(Operation.Add, outputRegister: 5, leftInputRegister: 3, rightInputRegister: 4, rightImmediateValue: 2)); // Calculate address
            AddInstructions(Instruction.NoOp(4));
            AddInstruction(Instruction.PushRegister(5));
        }

        private void PushMemory(int addressOffset = 0, int size = 1)
        {
            AddInstruction(Instruction.Pop(3)); // Address
            AddInstructions(Instruction.NoOp(4));

            CopyData(size,
                (offset, index) => Instruction.ReadMemory(outputRegister: 4 + index, addressRegister: 3, addressValue: offset + addressOffset),
                (offset, index) => Instruction.PushRegister(4 + index));
        }

        private void PopMemory(int addressOffset = 0, int size = 1)
        {
            if (size == 1)
            {
                AddInstruction(Instruction.Pop(3)); // Value
                AddInstruction(Instruction.Pop(4)); // Address
                AddInstructions(Instruction.NoOp(4));
                AddInstruction(Instruction.WriteMemory(addressRegister: 4, inputRegister: 3));
            }
            else
            {
                AddInstruction(Instruction.ReadStackValue(-size - 1, 3)); // Address

                CopyData(size,
                    (offset, index) => Instruction.Pop(4 + index),
                    (offset, index) => Instruction.WriteMemory(addressRegister: 3, addressValue: size - 1 - offset + addressOffset, inputRegister: 4 + index));

                AddInstruction(Instruction.AdjustStackPointer(-1)); // Pop off the address
            }
        }

        private void ReadArgument(int argumentIndex, int outputRegister)
        {
            if (methodContext.InlinedParameterValues.TryGetValue(argumentIndex, out var value))
            {
                AddInstruction(Instruction.SetRegisterToImmediateValue(outputRegister, value));
            }
            else
            {
                AddInstruction(Instruction.ReadStackValue(methodContext.Parameters[argumentIndex].Offset - methodContext.ParametersSize - (methodContext.IsInline ? 0 : 1) - methodContext.StackPointerOffset, outputRegister));
            }
        }

        private void PushArgument(int argumentIndex)
        {
            if (methodContext.InlinedParameterValues.TryGetValue(argumentIndex, out var value))
            {
                AddInstruction(Instruction.PushImmediateValue(value));
            }
            else
            {
                PushVariable(methodContext.Parameters[argumentIndex], -methodContext.ParametersSize - (methodContext.IsInline ? 0 : 1)); // Adjust to take into account the return address
            }
        }

        private void PopArgument(int argumentIndex)
        {
            PopVariable(methodContext.Parameters[argumentIndex], -methodContext.ParametersSize - (methodContext.IsInline ? 0 : 1)); // Adjust to take into account the return address
        }

        private void PushLocalVariable(int localIndex)
        {
            PushVariable(methodContext.LocalVariables[localIndex]);
        }

        private void PopLocalVariable(int localIndex)
        {
            PopVariable(methodContext.LocalVariables[localIndex]);
        }

        private void PopVariable(VariableInfo variable, int offsetRelativeToBase = 0)
        {
            // Start at the end of the variable since popping happens from right to left
            var baseOffset = variable.Offset + variable.Size - 1 + offsetRelativeToBase;

            CopyData(variable.Size,
                (offset, index) => Instruction.Pop(3 + index),
                (offset, index) => Instruction.WriteStackValue(baseOffset - methodContext.StackPointerOffset - offset, inputRegister: 3 + index));
        }

        private void PushVariable(VariableInfo variable, int offsetRelativeToBase = 0)
        {
            var baseOffset = variable.Offset + offsetRelativeToBase;

            CopyData(variable.Size,
                (offset, index) => Instruction.ReadStackValue(baseOffset - methodContext.StackPointerOffset + offset, outputRegister: 3 + index),
                (offset, index) => Instruction.PushRegister(3 + index));
        }

        private void CopyData(int size, Func<int, int, Instruction> read, Func<int, int, Instruction> write)
        {
            const int minChunkSize = 5; // The minimum chunk size to avoid needing no-ops
            int chunkSize;

            for (int chunkOffset = 0; chunkOffset < size; chunkOffset += chunkSize)
            {
                var remainingValues = size - chunkOffset;
                chunkSize = remainingValues < minChunkSize * 2 ? remainingValues : minChunkSize;

                for (int index = 0; index < chunkSize; index++)
                {
                    AddInstruction(read(chunkOffset + index, index));
                }

                AddInstructions(Instruction.NoOp(Math.Max(0, minChunkSize - chunkSize)));

                for (int index = 0; index < chunkSize; index++)
                {
                    AddInstruction(write(chunkOffset + index, index));
                }
            }
        }

        private bool AdjustStackPointer(int offset)
        {
            if (offset != 0)
            {
                AddInstruction(Instruction.AdjustStackPointer(offset));
                return true;
            }
            else
            {
                return false;
            }
        }

        private void AddBinaryOperation(Operation opCode)
        {
            AddInstruction(Instruction.Pop(3)); // Right operand
            AddInstruction(Instruction.Pop(4)); // Left operand
            AddInstructions(Instruction.NoOp(4));
            AddInstruction(Instruction.BinaryOperation(opCode, outputRegister: 5, leftInputRegister: 4, rightInputRegister: 3));
            AddInstructions(Instruction.NoOp(4));
            AddInstruction(Instruction.PushRegister(5));
        }

        private void AddComparison(ConditionOperator comparisonOperator, bool unsigned = false)
        {
            var unsignedAdjustment = unsigned ? int.MinValue : 0;

            AddInstruction(Instruction.Pop(3)); // Right operand
            AddInstruction(Instruction.Pop(4)); // Left operand
            AddInstructions(Instruction.NoOp(4));
            AddInstruction(Instruction.BinaryOperation(Operation.Subtract, outputRegister: 5, leftInputRegister: 4, rightInputRegister: 3, rightImmediateValue: unsignedAdjustment));
            AddInstructions(Instruction.NoOp(3));
            AddInstruction(Instruction.SetRegisterToImmediateValue(6, 0));
            AddInstruction(Instruction.SetRegister(6, immediateValue: 1, conditionLeftRegister: 5, conditionRightImmediateValue: unsignedAdjustment, conditionOperator: comparisonOperator));
            AddInstructions(Instruction.NoOp(4));
            AddInstruction(Instruction.PushRegister(6));
        }

        private void AddCallOrInlinedMethod(MethodBase method, Dictionary<int, int> inlinedParameterValues = null)
        {
            if (nonInlinedMethods.Contains(method))
            {
                AddInstruction(Instruction.Push(inputRegister: SpecialRegisters.InstructionPointer, immediateValue: 3)); // Takes into account push, jump and 4 no-ops to skip minus 3 cycles of overhead
                calls.Add((AddInstruction(Instruction.Jump(-(Instructions.Count + 1))), method));
                methodContext.StackPointerOffset -= GetMethodParameterInfo(method).Size + 1; // The `+ 1` is due to the return statement popping the return address off the stack
            }
            else
            {
                var calledMethodContext = AddMethod(method, isInline: true, inlinedParameterValues: inlinedParameterValues);
                methodContext.StackPointerOffset -= calledMethodContext.ParametersSize;
            }

            if (!method.IsVoid())
            {
                AddInstruction(Instruction.PushRegister(ReturnRegister));
            }
        }

        private void AddReturn()
        {
            if (methodContext.IsInline)
            {
                if (methodContext.IsVoid)
                {
                    AdjustStackPointer(-(methodContext.LocalVariablesSize + methodContext.ParametersSize));
                }
                else
                {
                    AddInstruction(Instruction.Pop(ReturnRegister, additionalStackPointerAdjustment: -(methodContext.LocalVariablesSize + methodContext.ParametersSize)));
                    AddInstructions(Instruction.NoOp(4));
                }
            }
            else
            {
                if (methodContext.IsVoid)
                {
                    Instruction.AdjustStackPointer(-methodContext.LocalVariablesSize);
                }
                else
                {
                    AddInstruction(Instruction.Pop(ReturnRegister, additionalStackPointerAdjustment: -methodContext.LocalVariablesSize));
                }

                AddInstruction(Instruction.Pop(SpecialRegisters.InstructionPointer, additionalStackPointerAdjustment: -methodContext.ParametersSize));
            }
        }

        private void AddJumpIf(int offset, ConditionOperator conditionOperator, List<(Instruction, int)> jumps)
        {
            AddInstruction(Instruction.Pop(3));
            AddInstructions(Instruction.NoOp(4));
            jumps.Add((AddInstruction(Instruction.JumpIf(-(Instructions.Count + 1), conditionLeftRegister: 3, conditionOperator: conditionOperator)), offset));
        }

        private void AddBinaryJumpIf(int offset, ConditionOperator conditionOperator, List<(Instruction, int)> jumps, bool unsigned = false)
        {
            var unsignedAdjustment = unsigned ? int.MinValue : 0;

            AddInstruction(Instruction.Pop(3)); // Right operand
            AddInstruction(Instruction.Pop(4)); // Left operand
            AddInstructions(Instruction.NoOp(4));
            AddInstruction(Instruction.BinaryOperation(Operation.Subtract, outputRegister: 5, leftInputRegister: 4, rightInputRegister: 3, rightImmediateValue: unsignedAdjustment));
            AddInstructions(Instruction.NoOp(4));

            jumps.Add((AddInstruction(Instruction.JumpIf(-(Instructions.Count + 1), conditionLeftRegister: 5, conditionRightImmediateValue: unsignedAdjustment, conditionOperator: conditionOperator)), offset));
        }

        private Instruction AddInstruction(Instruction instruction)
        {
            // Auto-increment has a side effect of adding to the values read out of registers, so we have to cancel that out
            if (instruction.OpCode != Operation.NoOp && instruction.AutoIncrement != 0)
            {
                instruction = new Instruction
                {
                    OpCode = instruction.OpCode,
                    OutputRegister = instruction.OutputRegister,
                    AutoIncrement = instruction.AutoIncrement,
                    LeftInputRegister = instruction.LeftInputRegister,
                    LeftImmediateValue = instruction.LeftImmediateValue - (instruction.LeftInputRegister != 0 ? instruction.AutoIncrement : 0),
                    RightInputRegister = instruction.RightInputRegister,
                    RightImmediateValue = instruction.RightImmediateValue - (instruction.RightInputRegister != 0 ? instruction.AutoIncrement : 0),
                    ConditionRegister = instruction.ConditionRegister,
                    ConditionImmediateValue = instruction.ConditionImmediateValue - (instruction.ConditionRegister != 0 ? instruction.AutoIncrement : 0),
                    ConditionOperator = instruction.ConditionOperator,
                    Comment = instruction.Comment
                };
            }

            Instructions.Add(instruction);

            if (instruction.LeftInputRegister == SpecialRegisters.StackPointer)
            {
                methodContext.StackPointerOffset += instruction.AutoIncrement;
            }

            if (instruction.OutputRegister == SpecialRegisters.InstructionPointer)
            {
                AddInstructions(Instruction.NoOp(6));
            }
            else if (instruction.LeftInputRegister == SpecialRegisters.InstructionPointer && instruction.AutoIncrement != 0)
            {
                AddInstructions(Instruction.NoOp(4));
            }

            return instruction;
        }

        private void AddInstructions(IEnumerable<Instruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                AddInstruction(instruction);
            }
        }

        private int AddData(IEnumerable<int> signals)
        {
            var reference = Data.Count + 1;

            Data.AddRange(signals
                .Select((signal, index) => (signal, index))
                .GroupBy(entry => entry.index / SignalUtils.MaxSignals)
                .OrderBy(entry => entry.Key)
                .Select(group => group.ToDictionary(entry => entry.index % SignalUtils.MaxSignals + 1, entry => entry.signal)));

            return reference;
        }

        private void SetData(int address, int index, int value) => Data[address - 1 + index / SignalUtils.MaxSignals][index % SignalUtils.MaxSignals + 1] = value;

        private static bool IsLoadSingleWordConstant(short opCodeValue)
        {
            return opCodeValue == OpCodes.Ldc_I4_M1.Value ||
                opCodeValue == OpCodes.Ldc_I4_0.Value ||
                opCodeValue == OpCodes.Ldc_I4_1.Value ||
                opCodeValue == OpCodes.Ldc_I4_2.Value ||
                opCodeValue == OpCodes.Ldc_I4_3.Value ||
                opCodeValue == OpCodes.Ldc_I4_4.Value ||
                opCodeValue == OpCodes.Ldc_I4_5.Value ||
                opCodeValue == OpCodes.Ldc_I4_6.Value ||
                opCodeValue == OpCodes.Ldc_I4_7.Value ||
                opCodeValue == OpCodes.Ldc_I4_8.Value ||
                opCodeValue == OpCodes.Ldc_I4.Value ||
                opCodeValue == OpCodes.Ldc_I4_S.Value ||
                opCodeValue == OpCodes.Ldc_R4.Value ||
                opCodeValue == OpCodes.Ldnull.Value;
        }

        private int GetConstantValue(ILInstruction ilInstruction)
        {
            var opCodeValue = ilInstruction.Code.Value;

            if (opCodeValue == OpCodes.Ldc_I4_M1.Value)
            {
                return -1;
            }
            else if (opCodeValue == OpCodes.Ldc_I4_0.Value)
            {
                return 0;
            }
            else if (opCodeValue == OpCodes.Ldc_I4_1.Value)
            {
                return 1;
            }
            else if (opCodeValue == OpCodes.Ldc_I4_2.Value)
            {
                return 2;
            }
            else if (opCodeValue == OpCodes.Ldc_I4_3.Value)
            {
                return 3;
            }
            else if (opCodeValue == OpCodes.Ldc_I4_4.Value)
            {
                return 4;
            }
            else if (opCodeValue == OpCodes.Ldc_I4_5.Value)
            {
                return 5;
            }
            else if (opCodeValue == OpCodes.Ldc_I4_6.Value)
            {
                return 6;
            }
            else if (opCodeValue == OpCodes.Ldc_I4_7.Value)
            {
                return 7;
            }
            else if (opCodeValue == OpCodes.Ldc_I4_8.Value)
            {
                return 8;
            }
            else if (opCodeValue == OpCodes.Ldc_I4.Value ||
                opCodeValue == OpCodes.Ldc_R4.Value)
            {
                return (int)ilInstruction.Operand;
            }
            else if (opCodeValue == OpCodes.Ldc_I4_S.Value)
            {
                return (sbyte)ilInstruction.Operand;
            }
            else if (opCodeValue == OpCodes.Ldnull.Value)
            {
                return 0;
            }
            else
            {
                throw new Exception($"Cannot get constant value from {ilInstruction.Code}");
            }
        }

        private static bool IsLoadArgument(short opCodeValue)
        {
            return opCodeValue == OpCodes.Ldarg_0.Value ||
                opCodeValue == OpCodes.Ldarg_1.Value ||
                opCodeValue == OpCodes.Ldarg_2.Value ||
                opCodeValue == OpCodes.Ldarg_3.Value ||
                opCodeValue == OpCodes.Ldarg.Value ||
                opCodeValue == OpCodes.Ldarg_S.Value;
        }

        public static int GetLoadArgumentPosition(ILInstruction ilInstruction)
        {
            var opCodeValue = ilInstruction.Code.Value;

            if (opCodeValue == OpCodes.Ldarg_0.Value)
            {
                return 0;
            }
            else if (opCodeValue == OpCodes.Ldarg_1.Value)
            {
                return 1;
            }
            else if (opCodeValue == OpCodes.Ldarg_2.Value)
            {
                return 2;
            }
            else if (opCodeValue == OpCodes.Ldarg_3.Value)
            {
                return 3;
            }
            else if (opCodeValue == OpCodes.Ldarg.Value)
            {
                return (int)ilInstruction.Operand;
            }
            else if (opCodeValue == OpCodes.Ldarg_S.Value)
            {
                return (byte)ilInstruction.Operand;
            }
            else
            {
                throw new Exception($"Cannot get argument position from {ilInstruction.Code}");
            }
        }

        private int? GetConstantValueForInlinedMethod(ILInstruction ilInstruction)
        {
            var opCodeValue = ilInstruction.Code.Value;

            if (IsLoadSingleWordConstant(opCodeValue))
            {
                return GetConstantValue(ilInstruction);
            }
            else if (IsLoadArgument(opCodeValue))
            {
                return methodContext.InlinedParameterValues.TryGetValue(GetLoadArgumentPosition(ilInstruction), out var value) ? value : (int?)null;
            }
            else
            {
                return null;
            }
        }

        private static bool IsLoadLocal(short opCodeValue)
        {
            return opCodeValue == OpCodes.Ldloc_0.Value ||
                opCodeValue == OpCodes.Ldloc_1.Value ||
                opCodeValue == OpCodes.Ldloc_2.Value ||
                opCodeValue == OpCodes.Ldloc_3.Value ||
                opCodeValue == OpCodes.Ldloc.Value ||
                opCodeValue == OpCodes.Ldloc_S.Value;
        }

        public static int GetLoadLocalIndex(ILInstruction ilInstruction)
        {
            var opCodeValue = ilInstruction.Code.Value;

            if (opCodeValue == OpCodes.Ldloc_0.Value)
            {
                return 0;
            }
            else if (opCodeValue == OpCodes.Ldloc_1.Value)
            {
                return 1;
            }
            else if (opCodeValue == OpCodes.Ldloc_2.Value)
            {
                return 2;
            }
            else if (opCodeValue == OpCodes.Ldloc_3.Value)
            {
                return 3;
            }
            else if (opCodeValue == OpCodes.Ldloc.Value)
            {
                return (int)ilInstruction.Operand;
            }
            else if (opCodeValue == OpCodes.Ldloc_S.Value)
            {
                return (byte)ilInstruction.Operand;
            }
            else
            {
                throw new Exception($"Cannot get local index from {ilInstruction.Code}");
            }
        }

        private static bool IsStoreLocal(short opCodeValue)
        {
            return opCodeValue == OpCodes.Stloc_0.Value ||
                opCodeValue == OpCodes.Stloc_1.Value ||
                opCodeValue == OpCodes.Stloc_2.Value ||
                opCodeValue == OpCodes.Stloc_3.Value ||
                opCodeValue == OpCodes.Stloc.Value ||
                opCodeValue == OpCodes.Stloc_S.Value;
        }

        public static int GetStoreLocalIndex(ILInstruction ilInstruction)
        {
            var opCodeValue = ilInstruction.Code.Value;

            if (opCodeValue == OpCodes.Stloc_0.Value)
            {
                return 0;
            }
            else if (opCodeValue == OpCodes.Stloc_1.Value)
            {
                return 1;
            }
            else if (opCodeValue == OpCodes.Stloc_2.Value)
            {
                return 2;
            }
            else if (opCodeValue == OpCodes.Stloc_3.Value)
            {
                return 3;
            }
            else if (opCodeValue == OpCodes.Stloc.Value)
            {
                return (int)ilInstruction.Operand;
            }
            else if (opCodeValue == OpCodes.Stloc_S.Value)
            {
                return (byte)ilInstruction.Operand;
            }
            else
            {
                throw new Exception($"Cannot get local index from {ilInstruction.Code}");
            }
        }

        private class MethodContext
        {
            public MethodBase Method { get; set; }
            public int InstructionIndex { get; set; }
            public bool IsInline { get; set; }
            public bool IsVoid { get; set; }
            public Dictionary<int, VariableInfo> Parameters { get; set; }
            public int ParametersSize { get; set; }
            public Dictionary<int, VariableInfo> LocalVariables { get; set; }
            public int LocalVariablesSize { get; set; }
            public MethodAnalysis Analysis { get; set; }
            public Dictionary<int, int> InlinedParameterValues { get; set; }
            public int StackPointerOffset { get; set; } = 0;
        }

        private class TypeInfo
        {
            public Dictionary<FieldInfo, VariableInfo> Fields { get; set; }
            public int Size { get; set; }
            public int RuntimeTypeReference { get; set; }
            public bool Initialize { get; set; }
            public ConstructorInfo StaticConstructor { get; set; }
        }

        private class MethodParameterInfo
        {
            public Dictionary<int, VariableInfo> Parameters { get; set; }
            public int Size { get; set; }
        }

        private class VariableInfo
        {
            public int Offset { get; set; }
            public int Size { get; set; }
        }
    }
}
