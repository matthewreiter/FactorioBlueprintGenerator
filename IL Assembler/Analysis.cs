using CompilerCommon;
using ILReader;
using FactoVision.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Assembler
{
    public partial class ProgramBuilder
    {
        private void Analyze(MethodInfo main)
        {
            var callCounts = new Dictionary<MethodBase, int>();
            var inlinedMethods = new HashSet<MethodBase>();
            var methodsToVisit = new Queue<MethodBase>();

            void AddType(Type type)
            {
                if (type == null)
                {
                    return;
                }

                if (types.Add(type))
                {
                    AddType(type.BaseType);
                }
            }

            void AddTypeInitializer(Type type)
            {
                var typeInfo = GetTypeInfo(type);

                if (!typeInfo.Initialize)
                {
                    typeInfo.Initialize = true;

                    var staticConstructor = type.GetConstructor(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

                    if (staticConstructor != null)
                    {
                        typeInfo.StaticConstructor = staticConstructor;
                        methodsToVisit.Enqueue(staticConstructor);
                    }
                }
            }

            methodsToVisit.Enqueue(main);
            callCounts[main] = 1;

            AddType(typeof(Type)); // Ensure that we have the "Type" type defined

            while (methodsToVisit.Count > 0)
            {
                var method = methodsToVisit.Dequeue();
                var isCompilerGenerated = IsCompilerGenerated(method);
                var ilInstructions = !isCompilerGenerated ? method.GetInstructions() : new List<ILInstruction> { };
                var methodBody = !isCompilerGenerated ? method.GetMethodBody() : null;
                var nonInlineableParameters = new HashSet<int>();
                var nonRegisterLocals = new HashSet<int>();
                var methodCalls = new HashSet<ILInstruction>();

                AddType(method.DeclaringType);

                if (method.GetCustomAttribute<InlineAttribute>() != null) // || ilInstructions.Count <= 10) // TODO: handle recursion
                {
                    inlinedMethods.Add(method);
                }

                foreach (var ilInstruction in ilInstructions)
                {
                    var opCodeValue = ilInstruction.Code.Value;

                    if (ilInstruction.Operand is Type typeOperand)
                    {
                        AddType(typeOperand);
                    }
                    else if (ilInstruction.Operand is FieldInfo fieldInfoOperand && ilInstruction.Code.Value != OpCodes.Ldtoken.Value)
                    {
                        AddType(fieldInfoOperand.DeclaringType);

                        // Only initialize the type if a static field is referenced. This isn't perfect since static initializers can do other things besides initialize static variables, but it reduces the amount of code that is generated.
                        if (fieldInfoOperand.IsStatic)
                        {
                            AddTypeInitializer(fieldInfoOperand.DeclaringType);
                        }
                    }

                    if (opCodeValue == OpCodes.Call.Value ||
                        opCodeValue == OpCodes.Callvirt.Value ||
                        opCodeValue == OpCodes.Newobj.Value)
                    {
                        var operand = (MethodBase)ilInstruction.Operand;

                        if (callCounts.ContainsKey(operand))
                        {
                            callCounts[operand]++;
                        }
                        else
                        {
                            callCounts[operand] = 1;
                            methodsToVisit.Enqueue(operand);
                        }

                        methodCalls.Add(ilInstruction);
                    }
                    else if (opCodeValue == OpCodes.Ldstr.Value)
                    {
                        AddType(typeof(string));
                    }
                    else if (opCodeValue == OpCodes.Newarr.Value)
                    {
                        var operand = (Type)ilInstruction.Operand;

                        AddType(operand.MakeArrayType());
                    }
                    else if (opCodeValue == OpCodes.Starg.Value ||
                      opCodeValue == OpCodes.Starg_S.Value ||
                      opCodeValue == OpCodes.Ldarga.Value ||
                      opCodeValue == OpCodes.Ldarga_S.Value)
                    {
                        var operand = Convert.ToInt32(ilInstruction.Operand);
                        nonInlineableParameters.Add(operand);
                    }
                    else if (opCodeValue == OpCodes.Ldloca.Value ||
                        opCodeValue == OpCodes.Ldloca_S.Value)
                    {
                        var operand = Convert.ToInt32(ilInstruction.Operand);
                        nonRegisterLocals.Add(operand);
                    }
                }

                var (sourceSinkMap, discontinuityMap) = AnalyzeStack(ilInstructions, methodBody);

                methodAnalyses[method] = new MethodAnalysis
                {
                    ILInstructions = ilInstructions,
                    IsCompilerGenerated = isCompilerGenerated,
                    SourceSinkMap = sourceSinkMap,
                    DiscontinuityMap = discontinuityMap,
                    NonInlineableParameters = nonInlineableParameters,
                    NonRegisterLocals = nonRegisterLocals,
                    MethodCalls = methodCalls
                };
            }

            nonInlinedMethods = callCounts
                .Where(entry => entry.Value > 1 && !inlinedMethods.Contains(entry.Key))
                .Select(entry => entry.Key)
                .ToHashSet();
        }

        /// <summary>
        /// Tracks the state of the stack over time to determine data flow between instructions that push values onto the stack and those that pop values off the stack.
        /// </summary>
        private static (Dictionary<ILInstruction, Sink> SourceSinkMap, Dictionary<ILInstruction, ILInstruction> DiscontinuityMap) AnalyzeStack(List<ILInstruction> ilInstructions, MethodBody methodBody)
        {
            var stack = ImmutableStack<ImmutableHashSet<ILInstruction>>.Empty;
            var stackSnapshots = new Dictionary<ILInstruction, ImmutableStack<ImmutableHashSet<ILInstruction>>>(); // Maps an instruction to all possible stacks at that point
            var sourceSinkMap = new Dictionary<ILInstruction, Sink>(); // Maps an instruction that pushes a value onto the stack with the instruction that pops it off
            var jumpSourceMap = new Dictionary<int, List<ILInstruction>>(); // Maps an instruction offset to a list of instructions that make forward jumps to it
            var discontinuityMap = new Dictionary<ILInstruction, ILInstruction>(); // Maps an instruction immediately after an unconditional forward jump to an instruction that makes a forward jump to it
            var isDiscontinuity = false; // Whether the previous instruction was an unconditional forward jump

            var exceptionHandlingClauses = methodBody?.ExceptionHandlingClauses;
            var exceptionHandlerOffsets = exceptionHandlingClauses != null
                ? exceptionHandlingClauses
                    .Where(clause => clause.Flags.HasFlag(ExceptionHandlingClauseOptions.Clause))
                    .Select(clause => clause.HandlerOffset)
                    .ToHashSet()
                : new HashSet<int>();

            foreach (var ilInstruction in ilInstructions)
            {
                var opCode = ilInstruction.Code;
                var opCodeValue = opCode.Value;
                var offset = ilInstruction.Offset;

                // Determine the number of values popped off the stack by the current instruction
                var popCount = opCode.StackBehaviourPop switch
                {
                    StackBehaviour.Pop1 => 1,
                    StackBehaviour.Pop1_pop1 => 2,
                    StackBehaviour.Popi => 1,
                    StackBehaviour.Popi_pop1 => 2,
                    StackBehaviour.Popi_popi => 2,
                    StackBehaviour.Popi_popi8 => 2,
                    StackBehaviour.Popi_popi_popi => 3,
                    StackBehaviour.Popi_popr4 => 2,
                    StackBehaviour.Popi_popr8 => 2,
                    StackBehaviour.Popref => 1,
                    StackBehaviour.Popref_pop1 => 2,
                    StackBehaviour.Popref_popi => 2,
                    StackBehaviour.Popref_popi_pop1 => 3,
                    StackBehaviour.Popref_popi_popi => 3,
                    StackBehaviour.Popref_popi_popi8 => 3,
                    StackBehaviour.Popref_popi_popr4 => 3,
                    StackBehaviour.Popref_popi_popr8 => 3,
                    StackBehaviour.Popref_popi_popref => 3,
                    _ => 0
                };

                // Determine the number of values pushed onto the stack by the current instruction
                var pushCount = opCode.StackBehaviourPush switch
                {
                    StackBehaviour.Push1 => 1,
                    StackBehaviour.Push1_push1 => 2,
                    StackBehaviour.Pushi => 1,
                    StackBehaviour.Pushi8 => 1,
                    StackBehaviour.Pushr4 => 1,
                    StackBehaviour.Pushr8 => 1,
                    StackBehaviour.Pushref => 1,
                    _ => 0
                };

                // If there are any instructions that make forward jumps to the current instruction, attempt to reconcile all possible stacks at this point
                if (jumpSourceMap.TryGetValue(offset, out var currentJumpSources))
                {
                    var stacks = new List<ImmutableStack<ImmutableHashSet<ILInstruction>>>();

                    if (isDiscontinuity)
                    {
                        discontinuityMap[ilInstruction] = currentJumpSources[0]; // If there are multiple jump sources it doesn't matter which one we pick
                    }
                    else
                    {
                        stacks.Add(stack);
                    }

                    foreach (var jumpSource in currentJumpSources)
                    {
                        if (stackSnapshots.TryGetValue(jumpSource, out var sourceStack))
                        {
                            stacks.Add(sourceStack);
                        }
                    }

                    stack = stacks.Aggregate(MergeStacks);
                }
                else if (isDiscontinuity)
                {
                    // Since the previous instruction jumps away and there are no forward jumps to this instruction,
                    // the only way to get here is a backwards jump. We should be able to assume that the stack is empty in this scenario.
                    stack = ImmutableStack<ImmutableHashSet<ILInstruction>>.Empty;
                    discontinuityMap[ilInstruction] = null;
                }

                // Thrown exceptions are pushed onto the stack before control is transferred to the exception handler
                if (exceptionHandlerOffsets.Contains(offset))
                {
                    stack = stack.Push(null);
                }

                // Handle values popped off the stack by the current instruction
                for (int index = 0; index < popCount; index++)
                {
                    stack = stack.Pop(out var sourceInstructions);

                    if (sourceInstructions != null)
                    {
                        var sink = new Sink { Instruction = ilInstruction };

                        foreach (var sourceInstruction in sourceInstructions)
                        {
                            sourceSinkMap[sourceInstruction] = sink;
                        }
                    }
                }

                // Handle values pushed onto the stack by the current instruction
                for (int index = 0; index < pushCount; index++)
                {
                    stack = stack.Push(ImmutableHashSet<ILInstruction>.Empty.Add(ilInstruction));
                }

                isDiscontinuity = false;

                if (opCode.FlowControl == FlowControl.Call)
                {
                    var operand = (MethodBase)ilInstruction.Operand;
                    var parameters = operand.GetParameters();
                    var parameterSources = new Dictionary<ParameterInfo, ILInstruction>();
                    var parameterOffset = operand.IsStatic ? 0 : 1;

                    foreach (var parameter in parameters.Reverse())
                    {
                        stack = stack.Pop(out var sourceInstructions);

                        var sink = new Sink { Instruction = ilInstruction, Parameter = parameter.Position + parameterOffset };

                        foreach (var sourceInstruction in sourceInstructions)
                        {
                            sourceSinkMap[sourceInstruction] = sink;
                        }
                    }

                    if (!operand.IsStatic && opCodeValue != OpCodes.Newobj.Value)
                    {
                        stack = stack.Pop();
                    }

                    if (!operand.IsVoid())
                    {
                        stack = stack.Push(ImmutableHashSet<ILInstruction>.Empty.Add(ilInstruction));
                    }
                }
                else if (opCode.FlowControl == FlowControl.Branch ||
                    opCode.FlowControl == FlowControl.Cond_Branch)
                {
                    var operand = Convert.ToInt32(ilInstruction.Operand);

                    // Record forward jumps so that we can determine the previous instruction even if the program flow jumps around
                    if (operand > 0)
                    {
                        if (!jumpSourceMap.TryGetValue(operand, out var jumpSources))
                        {
                            jumpSources = new List<ILInstruction>();
                            jumpSourceMap[operand] = jumpSources;
                        }

                        jumpSources.Add(ilInstruction);

                        if (opCode.FlowControl == FlowControl.Branch)
                        {
                            isDiscontinuity = true;
                        }
                    }
                }
                else if (opCode.FlowControl == FlowControl.Return ||
                    opCode.FlowControl == FlowControl.Throw)
                {
                    isDiscontinuity = true;
                }

                stackSnapshots[ilInstruction] = stack;
            }

            return (sourceSinkMap, discontinuityMap);
        }

        private static ImmutableStack<ImmutableHashSet<ILInstruction>> MergeStacks(ImmutableStack<ImmutableHashSet<ILInstruction>> stack1, ImmutableStack<ImmutableHashSet<ILInstruction>> stack2)
        {
            var tempStack = new Stack<ImmutableHashSet<ILInstruction>>();

            while (stack1 != stack2)
            {
                if (stack1.IsEmpty != stack2.IsEmpty)
                {
                    throw new Exception("Unable to reconcile the stack after a jump/branch/return.");
                }
                else if (stack1.IsEmpty)
                {
                    break;
                }

                stack1 = stack1.Pop(out var instructions1);
                stack2 = stack2.Pop(out var instructions2);

                tempStack.Push(instructions1.Concat(instructions2).ToImmutableHashSet());
            }

            while (tempStack.Count > 0)
            {
                stack1 = stack1.Push(tempStack.Pop());
            }

            return stack1;
        }

        private class MethodAnalysis
        {
            public List<ILInstruction> ILInstructions { get; set; }
            public bool IsCompilerGenerated { get; set; }
            public Dictionary<ILInstruction, Sink> SourceSinkMap { get; set; }
            public Dictionary<ILInstruction, ILInstruction> DiscontinuityMap { get; set; }
            public HashSet<int> NonInlineableParameters { get; set; }
            public HashSet<int> NonRegisterLocals { get; set; }
            public HashSet<ILInstruction> MethodCalls { get; set; }
        }

        private class Sink
        {
            public ILInstruction Instruction { get; set; }
            public int Parameter { get; set; }
        }
    }
}
