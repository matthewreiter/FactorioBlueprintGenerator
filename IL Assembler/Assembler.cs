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
            private MethodContext methodContext = new MethodContext();

            public void Build(Assembly assembly)
            {
                var main = assembly.GetTypes().Select(type => type.GetMethod("Main")).FirstOrDefault(main => main != null);

                if (main == null)
                {
                    throw new Exception("No main method found.");
                }

                // Initialize registers
                AddInstruction(Instruction.SetRegisterToImmediateValue(SpecialRegisters.StackPointer, RamAddress));
                AddInstructions(Enumerable.Range(3, RegisterCount - 2).Select(register => Instruction.SetRegisterToImmediateValue(register, 0)));

                // Clear the first 32 words of RAM
                AddInstructions(Enumerable.Range(RamAddress, 32).Select(address => Instruction.WriteMemory(addressValue: address, immediateValue: 0)));

                AddMethod(main, inline: true);
                //AddTestCode();

                // Jump back to the beginning
                AddInstruction(Instruction.Jump(-(Instructions.Count + 1)));
            }

            private void AddTestCode()
            {
                AddInstruction(Instruction.PushImmediateValue(5));
                AddInstruction(Instruction.PushImmediateValue(10));
                AddInstruction(Instruction.PushImmediateValue(15));
                AddInstruction(Instruction.Pop(3));
                AddInstruction(Instruction.Pop(4));
                AddInstruction(Instruction.Pop(5));
                AddInstructions(Instruction.NoOp(120));
            }

            private MethodContext AddMethod(MethodInfo method, bool inline = false)
            {
                var previousMethodContext = methodContext;
                methodContext = new MethodContext();
                try
                {
                    var parameters = method.GetParameters();
                    var ilInstructions = method.GetInstructions();
                    var methodBody = method.GetMethodBody();
                    var localVariables = methodBody.LocalVariables;
                    var instructionOffsetToIndexMap = new Dictionary<int, int>();
                    var jumps = new List<(Instruction, int)>();
                    var jumpIfs = new List<(Instruction, int)>();

                    AddInstruction(Instruction.AdjustStackPointer(localVariables.Count));

                    var ilInstructionIndex = 0;
                    foreach (var ilInstruction in ilInstructions)
                    {
                        instructionOffsetToIndexMap[ilInstruction.Offset] = Instructions.Count;

                        var opCodeValue = ilInstruction.Code.Value;

                        if (opCodeValue == OpCodes.Pop.Value)
                        {
                            AddInstruction(Instruction.AdjustStackPointer(-1));
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
                            PushStackValue(-parameters.Length);
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
                            AddInstruction(Instruction.Push(inputRegister: SpecialRegisters.StackPointer, (byte)ilInstruction.Operand - methodContext.StackPointerOffset));
                        }
                        else if (opCodeValue == OpCodes.Stind_I4.Value) // Pop value, pop address, then store value at address
                        {
                            AddInstruction(Instruction.Pop(3)); // Value
                            AddInstruction(Instruction.Pop(4)); // Address
                            AddInstructions(Instruction.NoOp(4));
                            AddInstruction(Instruction.WriteMemory(addressRegister: 4, inputRegister: 3));
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
                            jumpIfs.Add((AddInstruction(Instruction.JumpIf(-(Instructions.Count + 1), conditionLeftRegister: 3, conditionOperator: ConditionOperator.IsNotEqual)), operand));
                        }
                        else if (opCodeValue == OpCodes.Call.Value)
                        {
                            var operand = (MethodInfo)ilInstruction.Operand;

                            if (operand.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
                            {
                                AddCompilerGeneratedMethod(operand);
                            }
                            else if (operand.GetCustomAttribute<InlineAttribute>() != null)
                            {
                                var calledMethodContext = AddMethod(operand, inline: true);
                                methodContext.StackPointerOffset += calledMethodContext.StackPointerOffset;
                            }
                        }
                        else if (opCodeValue == OpCodes.Ret.Value)
                        {
                            if (method.ReturnType != typeof(void))
                            {
                                AddInstruction(Instruction.Pop(3, additionalStackPointerAdjustment: -(localVariables.Count + method.GetParameters().Length)));
                                AddInstructions(Instruction.NoOp(4));
                                AddInstruction(Instruction.PushRegister(3));
                            }

                            if (!inline)
                            {
                                throw new NotImplementedException("Cannot yet return from a non-inlined method");
                            }
                        }
                        else if (opCodeValue != OpCodes.Nop.Value)
                        {
                            throw new NotImplementedException($"Unsupported opcode {ilInstruction.Code.Name}");
                        }

                        ilInstructionIndex++;
                    }

                    foreach (var (instruction, offset) in jumps)
                    {
                        instruction.AutoIncrement += instructionOffsetToIndexMap[offset];
                    }

                    foreach (var (instruction, offset) in jumpIfs)
                    {
                        instruction.RightImmediateValue += instructionOffsetToIndexMap[offset];
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
                    AddInstruction(Instruction.Pop(3)); // Signal
                    AddInstruction(Instruction.Pop(4)); // Address
                    AddInstructions(Instruction.NoOp(4));
                    AddInstruction(Instruction.ReadSignal(outputRegister: 5, addressRegister: 4, signalRegister: 3));
                    AddInstructions(Instruction.NoOp(4));
                    AddInstruction(Instruction.PushRegister(5));
                }
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
