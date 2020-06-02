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
                Instructions = programBuilder.Instructions
            };
        }

        private class ProgramBuilder
        {
            public List<Instruction> Instructions { get; } = new List<Instruction>();

            private MethodContext methodContext = new MethodContext { InstructionIndex = 0 };
            private readonly Dictionary<MethodInfo, MethodContext> methodContexts = new Dictionary<MethodInfo, MethodContext>();
            private HashSet<MethodInfo> nonInlinedMethods;
            private readonly HashSet<Type> types = new HashSet<Type>();
            private readonly Dictionary<FieldInfo, int> staticFieldAddresses = new Dictionary<FieldInfo, int>();
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

                if (staticFieldAddresses.Count > 0)
                {
                    Console.WriteLine("Static field addresses:");
                    foreach (var entry in staticFieldAddresses)
                    {
                        Console.WriteLine($"{entry.Key.DeclaringType.Name}.{entry.Key.Name}: {entry.Value}");
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

                        if (opCodeValue == OpCodes.Ldsfld.Value || opCodeValue == OpCodes.Stsfld.Value)
                        {
                            var operand = (FieldInfo)ilInstruction.Operand;

                            types.Add(operand.DeclaringType);
                        }
                        else if (opCodeValue == OpCodes.Call.Value)
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
                        .Where(field => !field.IsLiteral))
                    {
                        staticFieldAddresses[field] = currentAddress++;
                    }
                }

                initialStackPointer = currentAddress;
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

                var previousMethodContext = methodContext;
                var parameters = method.GetParameters();
                var ilInstructions = method.GetInstructions();
                var methodBody = method.GetMethodBody();
                var localVariables = methodBody.LocalVariables;
                var instructionOffsetToIndexMap = new Dictionary<int, int>();
                var jumps = new List<(Instruction, int)>();

                methodContext = new MethodContext
                {
                    InstructionIndex = Instructions.Count,
                    IsInline = inline,
                    IsVoid = !(method is MethodInfo && ((MethodInfo)method).ReturnType != typeof(void)),
                    ParameterCount = parameters.Length,
                    LocalVariableCount = localVariables.Count
                };

                try
                {
                    AdjustStackPointer(localVariables.Count);

                    var ilInstructionIndex = 0;
                    foreach (var ilInstruction in ilInstructions)
                    {
                        instructionOffsetToIndexMap[ilInstruction.Offset] = Instructions.Count;

                        var opCodeValue = ilInstruction.Code.Value;

                        if (opCodeValue == OpCodes.Pop.Value)
                        {
                            AdjustStackPointer(-1);
                        }
                        else if (opCodeValue == OpCodes.Ldc_I4.Value)
                        {
                            AddInstruction(Instruction.PushImmediateValue((int)ilInstruction.Operand));
                        }
                        else if (opCodeValue == OpCodes.Ldc_I4_M1.Value)
                        {
                            AddInstruction(Instruction.PushImmediateValue(-1));
                        }
                        else if (opCodeValue == OpCodes.Ldc_I4_0.Value)
                        {
                            AddInstruction(Instruction.PushImmediateValue(0));
                        }
                        else if (opCodeValue == OpCodes.Ldc_I4_1.Value)
                        {
                            AddInstruction(Instruction.PushImmediateValue(1));
                        }
                        else if (opCodeValue == OpCodes.Ldc_I4_2.Value)
                        {
                            AddInstruction(Instruction.PushImmediateValue(2));
                        }
                        else if (opCodeValue == OpCodes.Ldc_I4_3.Value)
                        {
                            AddInstruction(Instruction.PushImmediateValue(3));
                        }
                        else if (opCodeValue == OpCodes.Ldc_I4_4.Value)
                        {
                            AddInstruction(Instruction.PushImmediateValue(4));
                        }
                        else if (opCodeValue == OpCodes.Ldc_I4_5.Value)
                        {
                            AddInstruction(Instruction.PushImmediateValue(5));
                        }
                        else if (opCodeValue == OpCodes.Ldc_I4_6.Value)
                        {
                            AddInstruction(Instruction.PushImmediateValue(6));
                        }
                        else if (opCodeValue == OpCodes.Ldc_I4_7.Value)
                        {
                            AddInstruction(Instruction.PushImmediateValue(7));
                        }
                        else if (opCodeValue == OpCodes.Ldc_I4_8.Value)
                        {
                            AddInstruction(Instruction.PushImmediateValue(8));
                        }
                        else if (opCodeValue == OpCodes.Ldc_I4_S.Value)
                        {
                            AddInstruction(Instruction.PushImmediateValue((sbyte)ilInstruction.Operand));
                        }
                        else if (opCodeValue == OpCodes.Ldarg_0.Value)
                        {
                            LoadArgument(0);
                        }
                        else if (opCodeValue == OpCodes.Ldarg_1.Value)
                        {
                            LoadArgument(1);
                        }
                        else if (opCodeValue == OpCodes.Ldarg_2.Value)
                        {
                            LoadArgument(2);
                        }
                        else if (opCodeValue == OpCodes.Ldarg_3.Value)
                        {
                            LoadArgument(3);
                        }
                        else if (opCodeValue == OpCodes.Ldarg_S.Value)
                        {
                            LoadArgument((byte)ilInstruction.Operand);
                        }
                        else if (opCodeValue == OpCodes.Ldloc_0.Value)
                        {
                            PushStackValue(0);
                        }
                        else if (opCodeValue == OpCodes.Ldloc_1.Value)
                        {
                            PushStackValue(1);
                        }
                        else if (opCodeValue == OpCodes.Ldloc_2.Value)
                        {
                            PushStackValue(2);
                        }
                        else if (opCodeValue == OpCodes.Ldloc_3.Value)
                        {
                            PushStackValue(3);
                        }
                        else if (opCodeValue == OpCodes.Ldloc_S.Value)
                        {
                            PushStackValue((byte)ilInstruction.Operand);
                        }
                        else if (opCodeValue == OpCodes.Stloc_0.Value)
                        {
                            PopStackValue(0);
                        }
                        else if (opCodeValue == OpCodes.Stloc_1.Value)
                        {
                            PopStackValue(1);
                        }
                        else if (opCodeValue == OpCodes.Stloc_2.Value)
                        {
                            PopStackValue(2);
                        }
                        else if (opCodeValue == OpCodes.Stloc_3.Value)
                        {
                            PopStackValue(3);
                        }
                        else if (opCodeValue == OpCodes.Stloc_S.Value)
                        {
                            PopStackValue((byte)ilInstruction.Operand);
                        }
                        else if (opCodeValue == OpCodes.Ldloca_S.Value)
                        {
                            AddInstruction(Instruction.Push(inputRegister: SpecialRegisters.StackPointer, immediateValue: (byte)ilInstruction.Operand - methodContext.StackPointerOffset));
                        }
                        else if (opCodeValue == OpCodes.Stind_I4.Value) // Pop value, pop address, then store value at address
                        {
                            AddInstruction(Instruction.Pop(3)); // Value
                            AddInstruction(Instruction.Pop(4)); // Address
                            AddInstructions(Instruction.NoOp(4));
                            AddInstruction(Instruction.WriteMemory(addressRegister: 4, inputRegister: 3));
                        }
                        else if (opCodeValue == OpCodes.Ldsfld.Value)
                        {
                            var operand = (FieldInfo)ilInstruction.Operand;

                            if (staticFieldAddresses.TryGetValue(operand, out var address))
                            {
                                AddInstruction(Instruction.ReadMemory(3, addressValue: address));
                                AddInstructions(Instruction.NoOp(4));
                                AddInstruction(Instruction.PushRegister(3));
                            }
                            else
                            {
                                throw new Exception($"Field {operand.DeclaringType}.{operand.Name} not allocated");
                            }
                        }
                        else if (opCodeValue == OpCodes.Stsfld.Value)
                        {
                            var operand = (FieldInfo)ilInstruction.Operand;

                            if (staticFieldAddresses.TryGetValue(operand, out var address))
                            {
                                AddInstruction(Instruction.Pop(3));
                                AddInstructions(Instruction.NoOp(4));
                                AddInstruction(Instruction.WriteMemory(addressValue: address, inputRegister: 3));
                            }
                            else
                            {
                                throw new Exception($"Field {operand.DeclaringType}.{operand.Name} not allocated");
                            }
                        }
                        else if (opCodeValue == OpCodes.Ldsflda.Value)
                        {
                            var operand = (FieldInfo)ilInstruction.Operand;

                            if (staticFieldAddresses.TryGetValue(operand, out var address))
                            {
                                AddInstruction(Instruction.PushImmediateValue(address));
                            }
                            else
                            {
                                throw new Exception($"Field {operand.DeclaringType}.{operand.Name} not allocated");
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

            private void LoadArgument(int argumentIndex)
            {
                PushStackValue(argumentIndex - methodContext.ParameterCount - (methodContext.IsInline ? 0 : 1)); // Adjust to take into account the return address
            }

            private void PopStackValue(int offsetRelativeToBase)
            {
                AddInstruction(Instruction.Pop(3));
                AddInstructions(Instruction.NoOp(4));
                AddInstruction(Instruction.WriteStackValue(offsetRelativeToBase - methodContext.StackPointerOffset, inputRegister: 3));
            }

            private void PushStackValue(int offsetRelativeToBase)
            {
                AddInstruction(Instruction.ReadStackValue(offsetRelativeToBase - methodContext.StackPointerOffset, outputRegister: 3));
                AddInstructions(Instruction.NoOp(4));
                AddInstruction(Instruction.PushRegister(3));
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

                methodContext.StackPointerOffset -= method.GetParameters().Length;

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
                        AdjustStackPointer(-(methodContext.LocalVariableCount + methodContext.ParameterCount));
                    }
                    else
                    {
                        AddInstruction(Instruction.Pop(ReturnRegister, additionalStackPointerAdjustment: -(methodContext.LocalVariableCount + methodContext.ParameterCount)));
                        AddInstructions(Instruction.NoOp(4));
                    }
                }
                else
                {
                    if (methodContext.IsVoid)
                    {
                        Instruction.AdjustStackPointer(-methodContext.LocalVariableCount);
                    }
                    else
                    {
                        AddInstruction(Instruction.Pop(ReturnRegister, additionalStackPointerAdjustment: -methodContext.LocalVariableCount));
                    }

                    AddInstruction(Instruction.Pop(SpecialRegisters.InstructionPointer, additionalStackPointerAdjustment: -methodContext.ParameterCount));
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
        }

        private class MethodContext
        {
            public int InstructionIndex { get; set; }
            public bool IsInline { get; set; }
            public bool IsVoid { get; set; }
            public int ParameterCount { get; set; }
            public int LocalVariableCount { get; set; }
            public int StackPointerOffset { get; set; } = 0;
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
