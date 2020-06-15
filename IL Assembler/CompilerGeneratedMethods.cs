using CompilerCommon;
using SeeSharp.Runtime;
using System.Reflection;

namespace Assembler
{
    public partial class ProgramBuilder
    {
        private void AddCompilerGeneratedMethod(MethodBase method)
        {
            if (method.DeclaringType == typeof(Memory))
            {
                if (method.Name == nameof(Memory.Read))
                {
                    AddInstruction(Instruction.Pop(4)); // Address
                    AddInstructions(Instruction.NoOp(4));
                    AddInstruction(Instruction.ReadMemory(outputRegister: ReturnRegister, addressRegister: 4));
                    AddInstructions(Instruction.NoOp(4));
                }
                else if (method.Name == nameof(Memory.Write))
                {
                    AddInstruction(Instruction.Pop(5)); // Value
                    AddInstruction(Instruction.Pop(4)); // Address
                    AddInstructions(Instruction.NoOp(4));
                    AddInstruction(Instruction.WriteMemory(addressRegister: 4, inputRegister: 5));
                }
                else if (method.Name == nameof(Memory.ReadSignal))
                {
                    AddInstruction(Instruction.Pop(5)); // Signal
                    AddInstruction(Instruction.Pop(4)); // Address
                    AddInstructions(Instruction.NoOp(4));
                    AddInstruction(Instruction.ReadSignal(outputRegister: ReturnRegister, addressRegister: 4, signalRegister: 5));
                    AddInstructions(Instruction.NoOp(4));
                }
            }
        }
    }
}
