using CompilerCommon;
using SeeSharp.Runtime;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Assembler
{
    public partial class ProgramBuilder
    {
        private Dictionary<Type, Dictionary<string, Action>> compilerGeneratedMethods;

        private void InitializeCompilerGeneratedMethods()
        {
            compilerGeneratedMethods = new Dictionary<Type, Dictionary<string, Action>>
            {
                {
                    typeof(Memory), new Dictionary<string, Action>
                    {
                        {
                            nameof(Memory.Read), () =>
                            {
                                ReadArgument(0, 4); // Address
                                AddInstructions(Instruction.NoOp(4));
                                AddInstruction(Instruction.ReadMemory(outputRegister: ReturnRegister, addressRegister: 4));
                                var adjustedStackPointer = AdjustStackPointer(-methodContext.ParametersSize);
                                AddInstructions(Instruction.NoOp(adjustedStackPointer ? 3 : 4));
                            }
                        },
                        {
                            nameof(Memory.Write), () =>
                            {
                                ReadArgument(0, 4); // Address
                                ReadArgument(1, 5); // Value
                                AddInstructions(Instruction.NoOp(4));
                                AddInstruction(Instruction.WriteMemory(addressRegister: 4, inputRegister: 5));
                                AddReturn();
                            }
                        },
                        {
                            nameof(Memory.ReadSignal), () =>
                            {
                                ReadArgument(0, 4); // Address
                                ReadArgument(1, 5); // Signal
                                AddInstructions(Instruction.NoOp(4));
                                AddInstruction(Instruction.ReadSignal(outputRegister: ReturnRegister, addressRegister: 4, signalRegister: 5));
                                var adjustedStackPointer = AdjustStackPointer(-methodContext.ParametersSize);
                                AddInstructions(Instruction.NoOp(adjustedStackPointer ? 3 : 4));
                            }
                        }
                    }
                },
                {
                    typeof(RuntimeHelpers), new Dictionary<string, Action>
                    {
                        {
                            nameof(RuntimeHelpers.InitializeArray), () =>
                            {
                                ReadArgument(0, outputRegister: 3); // Array
                                ReadArgument(1, outputRegister: 4); // Initial data
                                AddInstructions(Instruction.NoOp(3));
                                AddInstruction(Instruction.ReadMemory(5, addressRegister: 3, addressValue: 1)); // Initialize overall counter to array length

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
                                AddInstruction(Instruction.WriteMemory(addressRegister: 3, addressValue: 2, inputRegister: 8, autoIncrement: 1)); // Write initial data to the array
                                AddInstruction(Instruction.JumpIf(innerLoopOffset - (Instructions.Count + 1), conditionLeftRegister: 7, conditionRightImmediateValue: 0, ConditionOperator.GreaterThan)); // Jump to the beginning of the inner loop

                                // Outer loop end
                                AddInstruction(Instruction.IncrementRegister(4, 1)); // Increment the source pointer
                                AddInstruction(Instruction.JumpIf(outerLoopOffset - (Instructions.Count + 1), conditionLeftRegister: 5, conditionRightImmediateValue: 0, ConditionOperator.GreaterThan)); // Jump to the beginning of the outer loop
                                AddReturn();
                            }
                        }
                    }
                }
            };
        }

        private void AddCompilerGeneratedMethod(MethodBase method)
        {
            if (compilerGeneratedMethods.TryGetValue(method.DeclaringType, out var methodsOnClass) &&
                methodsOnClass.TryGetValue(method.Name, out var generateImplementation))
            {
                generateImplementation();
            }
        }

        private bool IsCompilerGenerated(MethodBase method)
        {
            return compilerGeneratedMethods.TryGetValue(method.DeclaringType, out var methodsOnClass) && methodsOnClass.ContainsKey(method.Name);
        }
    }
}
