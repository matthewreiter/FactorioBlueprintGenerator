using BlueprintCommon;
using BlueprintCommon.Models;
using CompilerCommon;
using ILReader;
using Microsoft.Extensions.Configuration;
using SeeSharp.Runtime.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Assembler
{
    public static class Assembler
    {
        private const int RegisterCount = 32;
        private const int RamAddress = 16385;
        private const int HeapAddress = RamAddress + 1024;
        private const int ReturnRegister = 3;

        public static void Run(IConfigurationRoot configuration)
        {
            Run(configuration.Get<AssemblerConfiguration>());
        }

        public static void Run(AssemblerConfiguration configuration)
        {
            var inputProgramFile = configuration.InputProgram;
            var outputBlueprintFile = configuration.OutputBlueprint;
            var outputJsonFile = configuration.OutputJson;
            //var outputInstructionsFile = configuration.OutputInstructions;
            var width = configuration.Width;
            var height = configuration.Height;

            var compiledProgram = AssembleCode(inputProgramFile);

            if (compiledProgram != null)
            {
                var blueprint = BlueprintGenerator.CreateBlueprintFromCompiledProgram(compiledProgram, width, height);
                BlueprintUtil.PopulateIndices(blueprint);

                var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };

                BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
                BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
                //WriteOutInstructions(outputInstructionsFile, blueprint);
            }
        }

        private static CompiledProgram AssembleCode(string inputProgramFile)
        {
            var assembly = Assembly.LoadFrom(inputProgramFile);

            var programBuilder = new ProgramBuilder();
            programBuilder.Build(assembly);

            return new CompiledProgram
            {
                Instructions = programBuilder.Instructions,
                Data = programBuilder.Data
            };
        }

        private class ProgramBuilder
        {
            public List<Instruction> Instructions { get; } = new List<Instruction>();
            public List<Dictionary<int, int>> Data { get; } = new List<Dictionary<int, int>>();

            private MethodContext methodContext = new MethodContext { InstructionIndex = 0 };
            private readonly Dictionary<MethodInfo, MethodContext> methodContexts = new Dictionary<MethodInfo, MethodContext>();
            private HashSet<MethodInfo> nonInlinedMethods;
            private readonly HashSet<Type> types = new HashSet<Type>();
            private readonly Dictionary<Type, TypeInfo> typeInfoCache = new Dictionary<Type, TypeInfo>();
            private readonly Dictionary<MethodBase, MethodParameterInfo> methodParameterInfoCache = new Dictionary<MethodBase, MethodParameterInfo>();
            private readonly Dictionary<FieldInfo, VariableInfo> staticFields = new Dictionary<FieldInfo, VariableInfo>();
            private readonly List<(Instruction, MethodInfo)> calls = new List<(Instruction, MethodInfo)>();
            private int initialStackPointer;

            public void Build(Assembly assembly)
            {
                var main = assembly.EntryPoint;

                Analyze(main);
                AllocateStaticFields();

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
                    instruction.SetJumpTarget(methodContexts[method].InstructionIndex);
                }

                if (nonInlinedMethods.Count > 0)
                {
                    Console.WriteLine("Method addresses:");
                    foreach (var method in nonInlinedMethods)
                    {
                        Console.WriteLine($"{method.DeclaringType.Name}.{method.Name}: {methodContexts[method].InstructionIndex + 1}");
                    }
                    Console.WriteLine();
                }

                if (staticFields.Count > 0)
                {
                    Console.WriteLine("Static field addresses:");
                    foreach (var entry in staticFields)
                    {
                        Console.WriteLine($"{entry.Key.DeclaringType.Name}.{entry.Key.Name}: {entry.Value.Offset}");
                    }
                    Console.WriteLine();
                }
            }

            private void Analyze(MethodInfo main)
            {
                var callCounts = new Dictionary<MethodInfo, int>();
                var inlinedMethods = new HashSet<MethodInfo>();
                var methodsToVisit = new Queue<MethodInfo>();

                methodsToVisit.Enqueue(main);
                callCounts[main] = 1;

                while (methodsToVisit.Count > 0)
                {
                    var method = methodsToVisit.Dequeue();
                    var ilInstructions = method.GetInstructions();

                    types.Add(method.DeclaringType);

                    if (method.GetCustomAttribute<CompilerGeneratedAttribute>() != null || method.GetCustomAttribute<InlineAttribute>() != null || ilInstructions.Count <= 10)
                    {
                        inlinedMethods.Add(method);
                    }

                    foreach (var ilInstruction in ilInstructions)
                    {
                        var opCodeValue = ilInstruction.Code.Value;

                        if (ilInstruction.Operand is Type)
                        {
                            types.Add((Type)ilInstruction.Operand);
                        }
                        else if (ilInstruction.Operand is FieldInfo)
                        {
                            var operand = (FieldInfo)ilInstruction.Operand;

                            types.Add(operand.DeclaringType);
                        }

                        if (opCodeValue == OpCodes.Call.Value)
                        {
                            var operand = (MethodInfo)ilInstruction.Operand;

                            if (callCounts.ContainsKey(operand))
                            {
                                callCounts[operand]++;
                            }
                            else
                            {
                                callCounts[operand] = 1;
                                methodsToVisit.Enqueue(operand);
                            }
                        }
                    }
                }

                nonInlinedMethods = callCounts
                    .Where(entry => entry.Value > 1 && !inlinedMethods.Contains(entry.Key))
                    .Select(entry => entry.Key)
                    .ToHashSet();
            }

            private void AllocateStaticFields()
            {
                var currentAddress = RamAddress;

                foreach (var type in types)
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

                initialStackPointer = currentAddress;
            }

            private (Dictionary<int, VariableInfo>, int) AllocateLocalVariables(IEnumerable<LocalVariableInfo> localVariables)
            {
                var variables = new Dictionary<int, VariableInfo>();
                var currentOffset = 0;

                foreach (var localVariable in localVariables)
                {
                    var size = GetVariableSize(localVariable.LocalType);
                    variables[localVariable.LocalIndex] = new VariableInfo { Offset = currentOffset, Size = size };
                    currentOffset += size;
                }

                return (variables, currentOffset);
            }

            private MethodParameterInfo GetMethodParameterInfo(MethodBase method)
            {
                if (!methodParameterInfoCache.TryGetValue(method, out var methodParameterInfo))
                {
                    var variables = new Dictionary<int, VariableInfo>();
                    var currentOffset = 0;

                    foreach (var parameter in method.GetParameters())
                    {
                        var size = GetVariableSize(parameter.ParameterType);
                        variables[parameter.Position] = new VariableInfo { Offset = currentOffset, Size = size };
                        currentOffset += size;
                    }

                    methodParameterInfo = new MethodParameterInfo { Parameters = variables, Size = currentOffset };
                }

                return methodParameterInfo;
            }

            private TypeInfo GetTypeInfo(Type type)
            {
                if (!typeInfoCache.TryGetValue(type, out var typeInfo))
                {
                    var fields = new Dictionary<FieldInfo, VariableInfo>();
                    var currentOffset = 0;

                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
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
                else
                {
                    return 1;
                }
            }

            private void InitializeTypes()
            {
                foreach (var type in types)
                {
                    var staticConstructor = type.GetConstructor(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

                    if (staticConstructor != null)
                    {
                        AddMethod(staticConstructor, inline: true);
                    }
                }
            }

            private MethodContext AddMethod(MethodBase method, bool inline = false)
            {
                // Stack frame layout:
                // Parameters
                // Return address (if inlined)
                // Local variables (stack pointer offset is relative to first local variable)
                // Evaluation stack

                if (method == ((Action<Array, RuntimeFieldHandle>)RuntimeHelpers.InitializeArray).Method)
                {
                    return AddArrayInitializer(method, inline);
                }

                var methodBody = method.GetMethodBody();

                if (methodBody == null)
                {
                    return null;
                }

                var previousMethodContext = methodContext;
                var ilInstructions = method.GetInstructions();
                var methodParameterInfo = GetMethodParameterInfo(method);
                var (localVariables, localVariablesSize) = AllocateLocalVariables(methodBody.LocalVariables);
                var instructionOffsetToIndexMap = new Dictionary<int, int>();
                var jumps = new List<(Instruction, int)>();

                methodContext = new MethodContext
                {
                    InstructionIndex = Instructions.Count,
                    IsInline = inline,
                    IsVoid = !(method is MethodInfo && ((MethodInfo)method).ReturnType != typeof(void)),
                    Parameters = methodParameterInfo.Parameters,
                    ParametersSize = methodParameterInfo.Size,
                    LocalVariables = localVariables,
                    LocalVariablesSize = localVariablesSize
                };

                try
                {
                    AdjustStackPointer(localVariablesSize);

                    var ilInstructionIndex = 0;
                    foreach (var ilInstruction in ilInstructions)
                    {
                        instructionOffsetToIndexMap[ilInstruction.Offset] = Instructions.Count;

                        var opCodeValue = ilInstruction.Code.Value;

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
                        else if (opCodeValue == OpCodes.Ldc_I4_M1.Value ||
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
                            opCodeValue == OpCodes.Ldc_I4_S.Value)
                        {
                            AddInstruction(Instruction.PushImmediateValue(GetConstantValue(ilInstruction)));
                        }
                        else if (opCodeValue == OpCodes.Ldarg_0.Value)
                        {
                            PushArgument(0);
                        }
                        else if (opCodeValue == OpCodes.Ldarg_1.Value)
                        {
                            PushArgument(1);
                        }
                        else if (opCodeValue == OpCodes.Ldarg_2.Value)
                        {
                            PushArgument(2);
                        }
                        else if (opCodeValue == OpCodes.Ldarg_3.Value)
                        {
                            PushArgument(3);
                        }
                        else if (opCodeValue == OpCodes.Ldarg_S.Value)
                        {
                            PushArgument((byte)ilInstruction.Operand);
                        }
                        else if (opCodeValue == OpCodes.Ldloc_0.Value)
                        {
                            PushLocalVariable(0);
                        }
                        else if (opCodeValue == OpCodes.Ldloc_1.Value)
                        {
                            PushLocalVariable(1);
                        }
                        else if (opCodeValue == OpCodes.Ldloc_2.Value)
                        {
                            PushLocalVariable(2);
                        }
                        else if (opCodeValue == OpCodes.Ldloc_3.Value)
                        {
                            PushLocalVariable(3);
                        }
                        else if (opCodeValue == OpCodes.Ldloc_S.Value)
                        {
                            PushLocalVariable((byte)ilInstruction.Operand);
                        }
                        else if (opCodeValue == OpCodes.Stloc_0.Value)
                        {
                            PopLocalVariable(0);
                        }
                        else if (opCodeValue == OpCodes.Stloc_1.Value)
                        {
                            PopLocalVariable(1);
                        }
                        else if (opCodeValue == OpCodes.Stloc_2.Value)
                        {
                            PopLocalVariable(2);
                        }
                        else if (opCodeValue == OpCodes.Stloc_3.Value)
                        {
                            PopLocalVariable(3);
                        }
                        else if (opCodeValue == OpCodes.Stloc_S.Value)
                        {
                            PopLocalVariable((byte)ilInstruction.Operand);
                        }
                        else if (opCodeValue == OpCodes.Ldloca_S.Value)
                        {
                            var operand = (byte)ilInstruction.Operand;

                            AddInstruction(Instruction.Push(inputRegister: SpecialRegisters.StackPointer, immediateValue: localVariables[operand].Offset - methodContext.StackPointerOffset));
                        }
                        else if (opCodeValue == OpCodes.Ldind_I4.Value)
                        {
                            AddInstruction(Instruction.Pop(4)); // Address
                            AddInstructions(Instruction.NoOp(4));
                            AddInstruction(Instruction.ReadMemory(3, addressRegister: 4));
                            AddInstructions(Instruction.NoOp(4));
                            AddInstruction(Instruction.PushRegister(3));
                        }
                        else if (opCodeValue == OpCodes.Stind_I4.Value)
                        {
                            AddInstruction(Instruction.Pop(3)); // Value
                            AddInstruction(Instruction.Pop(4)); // Address
                            AddInstructions(Instruction.NoOp(4));
                            AddInstruction(Instruction.WriteMemory(addressRegister: 4, inputRegister: 3));
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
                        else if (opCodeValue == OpCodes.Stfld.Value)
                        {
                            var operand = (FieldInfo)ilInstruction.Operand;
                            var typeInfo = GetTypeInfo(operand.DeclaringType);
                            var field = typeInfo.Fields[operand];

                            if (field.Size == 1)
                            {
                                AddInstruction(Instruction.Pop(4)); // Value
                                AddInstruction(Instruction.Pop(3)); // Pointer to beginning of structure
                                AddInstructions(Instruction.NoOp(4));
                                AddInstruction(Instruction.WriteMemory(addressRegister: 3, addressValue: field.Offset, inputRegister: 4));
                            }
                            else
                            {
                                AddInstruction(Instruction.ReadStackValue(-field.Size - 1, 3)); // Pointer to beginning of structure

                                // Start at the end of the variable since popping happens from right to left
                                var baseOffset = field.Offset + field.Size - 1;

                                CopyData(field.Size,
                                    (offset, index) => Instruction.Pop(4 + index),
                                    (offset, index) => Instruction.WriteMemory(addressRegister: 3, addressValue: baseOffset - offset, inputRegister: 4 + index));

                                AddInstruction(Instruction.AdjustStackPointer(-1)); // Pop off the structure pointer
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
                            var typeInfo = GetTypeInfo(operand);

                            PushArrayElement(typeInfo.Size);
                        }
                        else if (opCodeValue == OpCodes.Ldelem_I4.Value)
                        {
                            PushArrayElement(1);
                        }
                        else if (opCodeValue == OpCodes.Stelem.Value)
                        {
                            var operand = (Type)ilInstruction.Operand;
                            var typeInfo = GetTypeInfo(operand);

                            PopArrayElement(typeInfo.Size);
                        }
                        else if (opCodeValue == OpCodes.Stelem_I4.Value)
                        {
                            PopArrayElement(1);
                        }
                        else if (opCodeValue == OpCodes.Ldelema.Value)
                        {
                            var operand = (Type)ilInstruction.Operand;
                            var typeInfo = GetTypeInfo(operand);

                            PushArrayElementAddress(typeInfo.Size);
                        }
                        else if (opCodeValue == OpCodes.Initobj.Value)
                        {
                            var operand = (Type)ilInstruction.Operand;
                            var typeInfo = GetTypeInfo(operand);

                            AddInstruction(Instruction.Pop(3));
                            AddInstructions(Instruction.NoOp(4));

                            for (int index = 0; index < typeInfo.Size; index++)
                            {
                                AddInstruction(Instruction.WriteMemory(addressRegister: 3, addressValue: index, immediateValue: 0));
                            }
                        }
                        else if (opCodeValue == OpCodes.Newarr.Value)
                        {
                            var operand = (Type)ilInstruction.Operand;
                            var size = GetTypeInfo(operand).Size;

                            AddInstruction(Instruction.Pop(3)); // Array length
                            AddInstruction(Instruction.ReadMemory(4, addressValue: HeapAddress));

                            // Calculate the total size of the array
                            int arraySizeRegister;
                            if (size != 1)
                            {
                                arraySizeRegister = 7;
                                AddInstructions(Instruction.NoOp(3));
                                AddInstruction(Instruction.BinaryOperation(Operation.Multiply, outputRegister: arraySizeRegister, leftInputRegister: 3, rightImmediateValue: size));
                            }
                            else
                            {
                                arraySizeRegister = 3;
                            }

                            AddInstructions(Instruction.NoOp(4));
                            AddInstruction(Instruction.BinaryOperation(Operation.Add, outputRegister: 5, leftInputRegister: 4, rightInputRegister: arraySizeRegister, rightImmediateValue: 1)); // Calculate the new free pointer
                            AddInstruction(Instruction.SetRegister(6, inputRegister: 4, immediateValue: 1)); // Initialize the pointer for clearing the array, leaving room for the array length
                            AddInstruction(Instruction.SetRegister(7, inputRegister: arraySizeRegister)); // Initialize the (decrementing) counter for clearing the array
                            AddInstruction(Instruction.PushRegister(4)); // Push the array reference onto the stack
                            AddInstruction(Instruction.WriteMemory(addressRegister: 4, inputRegister: 3)); // Write the array length to the beginning of the array
                            AddInstruction(Instruction.WriteMemory(addressValue: HeapAddress, inputRegister: 5)); // Allocate the array on the heap

                            // Loop to clear array
                            AddInstruction(Instruction.WriteMemory(addressRegister: 6, immediateValue: 0, autoIncrement: 1));
                            AddInstruction(Instruction.IncrementRegister(7, -1));
                            AddInstruction(Instruction.JumpIf(-3, conditionLeftRegister: 7, conditionOperator: ConditionOperator.GreaterThan));
                        }
                        else if (opCodeValue == OpCodes.Ldtoken.Value)
                        {
                            var operand = (FieldInfo)ilInstruction.Operand;

                            var arraySizeInstruction = ilInstructions[ilInstructionIndex - 3];
                            var newArrayInstruction = ilInstructions[ilInstructionIndex - 2];

                            int arraySize = GetConstantValue(arraySizeInstruction);
                            var array = Array.CreateInstance((Type)newArrayInstruction.Operand, arraySize);
                            RuntimeHelpers.InitializeArray(array, operand.FieldHandle);

                            AddInstruction(Instruction.PushImmediateValue(Data.Count + 1)); // Push the token reference onto the stack

                            int chunkSize;
                            for (int chunkOffset = 0; chunkOffset < arraySize; chunkOffset += chunkSize)
                            {
                                chunkSize = Math.Min(SignalUtils.MaxSignals, arraySize - chunkOffset);
                                Data.Add(Enumerable.Range(0, chunkSize).ToDictionary(index => index + 1, index => (int)array.GetValue(chunkOffset + index)));
                            }
                        }
                        else if (opCodeValue == OpCodes.Not.Value)
                        {
                            AddInstruction(Instruction.Pop(3));
                            AddInstructions(Instruction.NoOp(4));
                            AddInstruction(Instruction.BinaryOperation(Operation.Xor, 4, leftInputRegister: 3, rightImmediateValue: -1));
                            AddInstructions(Instruction.NoOp(4));
                            AddInstruction(Instruction.Push(4));
                        }
                        else if (opCodeValue == OpCodes.Mul.Value)
                        {
                            AddBinaryOperation(Operation.Multiply);
                        }
                        else if (opCodeValue == OpCodes.Div.Value)
                        {
                            AddBinaryOperation(Operation.Divide);
                        }
                        else if (opCodeValue == OpCodes.Add.Value)
                        {
                            AddBinaryOperation(Operation.Add);
                        }
                        else if (opCodeValue == OpCodes.Sub.Value)
                        {
                            AddBinaryOperation(Operation.Subtract);
                        }
                        else if (opCodeValue == OpCodes.Rem.Value)
                        {
                            AddBinaryOperation(Operation.Mod);
                        }
                        else if (opCodeValue == OpCodes.Shl.Value)
                        {
                            AddBinaryOperation(Operation.LeftShift);
                        }
                        else if (opCodeValue == OpCodes.Shr.Value)
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
                        else if (opCodeValue == OpCodes.Clt.Value)
                        {
                            AddComparison(ConditionOperator.LessThan);
                        }
                        else if (opCodeValue == OpCodes.Br_S.Value)
                        {
                            var operand = (int)ilInstruction.Operand;

                            if (operand != ilInstructions.ElementAtOrDefault(ilInstructionIndex + 1)?.Offset)
                            {
                                jumps.Add((AddInstruction(Instruction.Jump(-(Instructions.Count + 1))), operand));
                            }
                        }
                        else if (opCodeValue == OpCodes.Brtrue_S.Value)
                        {
                            var operand = (int)ilInstruction.Operand;

                            AddInstruction(Instruction.Pop(3));
                            AddInstructions(Instruction.NoOp(4));
                            jumps.Add((AddInstruction(Instruction.JumpIf(-(Instructions.Count + 1), conditionLeftRegister: 3, conditionOperator: ConditionOperator.IsNotEqual)), operand));
                        }
                        else if (opCodeValue == OpCodes.Call.Value)
                        {
                            var operand = (MethodInfo)ilInstruction.Operand;

                            AddCallOrInlinedMethod(operand);
                        }
                        else if (opCodeValue == OpCodes.Ret.Value)
                        {
                            AddReturn();
                        }
                        else if (opCodeValue != OpCodes.Nop.Value)
                        {
                            throw new NotImplementedException($"Unsupported opcode {ilInstruction.Code.Name}");
                        }

                        ilInstructionIndex++;
                    }

                    foreach (var (instruction, offset) in jumps)
                    {
                        instruction.SetJumpTarget(instructionOffsetToIndexMap[offset]);
                    }

                    return methodContext;
                }
                finally
                {
                    methodContext = previousMethodContext;
                }
            }

            private void AddCompilerGeneratedMethod(MethodInfo method)
            {
                if (method.DeclaringType.Name == "Memory" && method.Name == "ReadSignal")
                {
                    AddInstruction(Instruction.Pop(5)); // Signal
                    AddInstruction(Instruction.Pop(4)); // Address
                    AddInstructions(Instruction.NoOp(4));
                    AddInstruction(Instruction.ReadSignal(outputRegister: ReturnRegister, addressRegister: 4, signalRegister: 5));
                    AddInstructions(Instruction.NoOp(4));
                }
            }

            private MethodContext AddArrayInitializer(MethodBase method, bool inline)
            {
                var previousMethodContext = methodContext;
                var methodParameterInfo = GetMethodParameterInfo(method);

                methodContext = new MethodContext
                {
                    InstructionIndex = Instructions.Count,
                    IsInline = inline,
                    IsVoid = true,
                    Parameters = methodParameterInfo.Parameters,
                    ParametersSize = methodParameterInfo.Size
                };

                try
                {
                    ReadArgument(0, outputRegister: 3); // Array
                    ReadArgument(1, outputRegister: 4); // Initial data
                    AddInstructions(Instruction.NoOp(3));
                    AddInstruction(Instruction.ReadMemory(5, addressRegister: 3)); // Initialize overall counter to array length

                    // Outer loop beginning
                    var outerLoopOffset = Instructions.Count;
                    AddInstruction(Instruction.SetRegisterToImmediateValue(6, 1)); // Initialize signal counter
                    AddInstruction(Instruction.SetRegisterToImmediateValue(7, SignalUtils.MaxSignals)); // Initialize the inner loop count
                    AddInstructions(Instruction.NoOp(2));
                    AddInstruction(Instruction.SetRegister(7, inputRegister: 5, conditionLeftRegister: 5, conditionRightImmediateValue: SignalUtils.MaxSignals, conditionOperator: ConditionOperator.LessThan)); // If there is less data remaining than the signal count, use that instead

                    // Inner loop
                    var innerLoopOffset = Instructions.Count;
                    AddInstruction(Instruction.ReadSignal(outputRegister: 8, addressRegister: 4, signalRegister: 6)); // Read initial data from ROM
                    AddInstruction(Instruction.IncrementRegister(5, -1)); // Decrement the overall counter
                    AddInstruction(Instruction.IncrementRegister(6, 1)); // Increment the signal counter
                    AddInstructions(Instruction.NoOp(1));
                    AddInstruction(Instruction.IncrementRegister(7, -1)); // Decrement the inner loop counter
                    AddInstruction(Instruction.WriteMemory(addressRegister: 3, addressValue: 1, inputRegister: 8, autoIncrement: 1)); // Write initial data to the array
                    AddInstruction(Instruction.JumpIf(innerLoopOffset - (Instructions.Count + 1), conditionLeftRegister: 7, conditionRightImmediateValue: 0, ConditionOperator.GreaterThan)); // Jump to the beginning of the inner loop

                    // Outer loop end
                    AddInstruction(Instruction.IncrementRegister(4, 1)); // Increment the source pointer
                    AddInstruction(Instruction.JumpIf(outerLoopOffset - (Instructions.Count + 1), conditionLeftRegister: 5, conditionRightImmediateValue: 0, ConditionOperator.GreaterThan)); // Jump to the beginning of the outer loop

                    AddReturn();

                    return methodContext;
                }
                finally
                {
                    methodContext = previousMethodContext;
                }
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

                AddInstruction(Instruction.BinaryOperation(Operation.Add, outputRegister: 5, leftInputRegister: 3, rightInputRegister: 4, rightImmediateValue: 1)); // Calculate starting address
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

                    AddInstruction(Instruction.BinaryOperation(Operation.Add, outputRegister: 5, leftInputRegister: 3, rightInputRegister: 4, rightImmediateValue: 1)); // Calculate address
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
                    AddInstruction(Instruction.BinaryOperation(Operation.Add, outputRegister: 5, leftInputRegister: 3, rightInputRegister: 4, rightImmediateValue: size)); // Calculate starting address
                    AddInstructions(Instruction.NoOp(4));

                    CopyData(size,
                        (offset, index) => Instruction.Pop(6 + index),
                        (offset, index) => Instruction.WriteMemory(addressRegister: 5, addressValue: -offset, inputRegister: 6 + index));

                    AddInstruction(Instruction.AdjustStackPointer(-2)); // Pop off the index and array
                }
            }

            private void PushArrayElementAddress(int size)
            {
                if (size == 1)
                {
                    AddInstruction(Instruction.Pop(4)); // Index
                    AddInstruction(Instruction.Pop(3)); // Array
                    AddInstructions(Instruction.NoOp(4));

                    // TODO: check bounds

                    AddInstruction(Instruction.BinaryOperation(Operation.Add, outputRegister: 5, leftInputRegister: 3, rightInputRegister: 4, rightImmediateValue: 1)); // Calculate address
                    AddInstructions(Instruction.NoOp(4));
                    AddInstruction(Instruction.PushRegister(5));
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            private void ReadArgument(int argumentIndex, int outputRegister)
            {
                AddInstruction(Instruction.ReadStackValue(methodContext.Parameters[argumentIndex].Offset - methodContext.ParametersSize - (methodContext.IsInline ? 0 : 1) - methodContext.StackPointerOffset, outputRegister));
            }

            private void PushArgument(int argumentIndex)
            {
                PushVariable(methodContext.Parameters[argumentIndex], -methodContext.ParametersSize - (methodContext.IsInline ? 0 : 1)); // Adjust to take into account the return address
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

            private void AdjustStackPointer(int offset)
            {
                if (offset != 0)
                {
                    AddInstruction(Instruction.AdjustStackPointer(offset));
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

            private void AddComparison(ConditionOperator comparisonOperator)
            {
                AddInstruction(Instruction.Pop(3)); // Right operand
                AddInstruction(Instruction.Pop(4)); // Left operand
                AddInstructions(Instruction.NoOp(4));
                AddInstruction(Instruction.BinaryOperation(Operation.Subtract, outputRegister: 5, leftInputRegister: 4, rightInputRegister: 3));
                AddInstructions(Instruction.NoOp(3));
                AddInstruction(Instruction.SetRegisterToImmediateValue(6, 0));
                AddInstruction(Instruction.SetRegister(6, immediateValue: 1, conditionLeftRegister: 5, conditionOperator: comparisonOperator));
                AddInstructions(Instruction.NoOp(4));
                AddInstruction(Instruction.PushRegister(6));
            }

            private void AddCallOrInlinedMethod(MethodInfo method)
            {
                if (nonInlinedMethods.Contains(method))
                {
                    AddInstruction(Instruction.Push(inputRegister: SpecialRegisters.InstructionPointer, immediateValue: 3)); // Takes into account push, jump and 4 no-ops to skip minus 3 cycles of overhead
                    calls.Add((AddInstruction(Instruction.Jump(-(Instructions.Count + 1))), method));
                    methodContext.StackPointerOffset--; // Record the effect of the return statement popping the return address off the stack
                }
                else if (method.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
                {
                    AddCompilerGeneratedMethod(method);
                }
                else
                {
                    AddMethod(method, inline: true);
                }

                methodContext.StackPointerOffset -= GetMethodParameterInfo(method).Size;

                if (method.ReturnType != typeof(void))
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
                        ConditionOperator = instruction.ConditionOperator
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
                else if (opCodeValue == OpCodes.Ldc_I4.Value)
                {
                    return (int)ilInstruction.Operand;
                }
                else if (opCodeValue == OpCodes.Ldc_I4_S.Value)
                {
                    return (sbyte)ilInstruction.Operand;
                }
                else
                {
                    throw new Exception($"Cannot get constant value from {ilInstruction.Code}");
                }
            }
        }

        private class MethodContext
        {
            public int InstructionIndex { get; set; }
            public bool IsInline { get; set; }
            public bool IsVoid { get; set; }
            public Dictionary<int, VariableInfo> Parameters { get; set; }
            public int ParametersSize { get; set; }
            public Dictionary<int, VariableInfo> LocalVariables { get; set; }
            public int LocalVariablesSize { get; set; }
            public int StackPointerOffset { get; set; } = 0;
        }

        private class TypeInfo
        {
            public Dictionary<FieldInfo, VariableInfo> Fields { get; set; }
            public int Size { get; set; }
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

    public class AssemblerConfiguration
    {
        public string InputProgram { get; set; }
        public string OutputBlueprint { get; set; }
        public string OutputJson { get; set; }
        public string OutputInstructions { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }
}
