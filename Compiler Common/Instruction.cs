using System.Collections.Generic;
using System.Linq;

namespace CompilerCommon
{
    public class Instruction
    {
        public Operation OpCode { get; set; }
        public int OutputRegister { get; set; }
        public int AutoIncrement { get; set; }
        public int LeftInputRegister { get; set; }
        public int LeftImmediateValue { get; set; }
        public int RightInputRegister { get; set; }
        public int RightImmediateValue { get; set; }
        public int ConditionRegister { get; set; }
        public int ConditionImmediateValue { get; set; }
        public ConditionOperator ConditionOperator { get; set; }

        public static IEnumerable<Instruction> NoOp(int cycles) => Enumerable.Repeat(new Instruction { OpCode = Operation.NoOp }, cycles);

        public static Instruction SetRegisterToImmediateValue(int outputRegister, int immediateValue) => SetRegister(outputRegister, immediateValue: immediateValue);

        public static Instruction SetRegister(int outputRegister, int inputRegister = 0, int immediateValue = 0, int conditionLeftRegister = 0, int conditionRightImmediateValue = 0, ConditionOperator conditionOperator = ConditionOperator.IsEqual) => new Instruction
        {
            OpCode = Operation.Add,
            OutputRegister = outputRegister,
            LeftInputRegister = inputRegister,
            LeftImmediateValue = immediateValue,
            ConditionRegister = conditionLeftRegister,
            ConditionImmediateValue = -conditionRightImmediateValue,
            ConditionOperator = conditionOperator
        };

        public static Instruction Pop(int outputRegister, int additionalStackPointerAdjustment = 0) => new Instruction
        {
            OpCode = Operation.Read,
            OutputRegister = outputRegister,
            AutoIncrement = -1 + additionalStackPointerAdjustment,
            LeftInputRegister = SpecialRegisters.StackPointer,
            LeftImmediateValue = -1,
            RightImmediateValue = 1
        };

        public static Instruction PushImmediateValue(int value) => Push(immediateValue: value);

        public static Instruction PushRegister(int inputRegister) => Push(inputRegister: inputRegister);

        public static Instruction Push(int inputRegister = 0, int immediateValue = 0) => new Instruction
        {
            OpCode = Operation.Write,
            AutoIncrement = 1,
            LeftInputRegister = SpecialRegisters.StackPointer,
            RightInputRegister = inputRegister,
            RightImmediateValue = immediateValue
        };

        public static Instruction AdjustStackPointer(int offset) => new Instruction
        {
            OpCode = Operation.NoOp,
            AutoIncrement = offset,
            LeftInputRegister = SpecialRegisters.StackPointer
        };

        public static Instruction ReadStackValue(int offset, int outputRegister) => ReadMemory(outputRegister, SpecialRegisters.StackPointer, offset);

        public static Instruction WriteStackValue(int offset, int inputRegister = 0, int immediateValue = 0) => WriteMemory(SpecialRegisters.StackPointer, offset, inputRegister, immediateValue);

        public static Instruction ReadMemory(int outputRegister, int addressRegister = 0, int addressValue = 0) => ReadSignal(outputRegister, addressRegister, addressValue, signalValue: 1);

        public static Instruction WriteMemory(int addressRegister = 0, int addressValue = 0, int inputRegister = 0, int immediateValue = 0) => new Instruction
        {
            OpCode = Operation.Write,
            LeftInputRegister = addressRegister,
            LeftImmediateValue = addressValue,
            RightInputRegister = inputRegister,
            RightImmediateValue = immediateValue
        };

        public static Instruction ReadSignal(int outputRegister, int addressRegister = 0, int addressValue = 0, int signalRegister = 0, int signalValue = 0) => new Instruction
        {
            OpCode = Operation.Read,
            OutputRegister = outputRegister,
            LeftInputRegister = addressRegister,
            LeftImmediateValue = addressValue,
            RightInputRegister = signalRegister,
            RightImmediateValue = signalValue
        };

        public static Instruction BinaryOperation(Operation opCode, int outputRegister, int leftInputRegister = 0, int leftImmediateValue = 0, int rightInputRegister = 0, int rightImmediateValue = 0) => new Instruction
        {
            OpCode = opCode,
            OutputRegister = outputRegister,
            LeftInputRegister = leftInputRegister,
            LeftImmediateValue = leftImmediateValue,
            RightInputRegister = rightInputRegister,
            RightImmediateValue = rightImmediateValue
        };

        public static Instruction Jump(int offset) => new Instruction
        {
            OpCode = Operation.NoOp,
            AutoIncrement = offset - 2, // Account for the delay between reading the instruction and the address getting incremented
            LeftInputRegister = SpecialRegisters.InstructionPointer
        };

        public static Instruction JumpIf(int offset, int conditionLeftRegister = 0, int conditionRightImmediateValue = 0, ConditionOperator conditionOperator = ConditionOperator.IsEqual) => new Instruction
        {
            OpCode = Operation.Add,
            OutputRegister = SpecialRegisters.InstructionPointer,
            LeftInputRegister = SpecialRegisters.InstructionPointer,
            RightImmediateValue = offset - 2, // Account for the delay between reading the instruction and the address getting read in
            ConditionRegister = conditionLeftRegister,
            ConditionImmediateValue = -conditionRightImmediateValue,
            ConditionOperator = conditionOperator
        };
    }

    public enum Operation
    {
        NoOp,
        Multiply,
        Divide,
        Add,
        Subtract,
        Mod,
        Power,
        LeftShift,
        RightShift,
        And,
        Or,
        Xor,
        Note,
        Chord,
        Read,
        Write
    }

    public enum ConditionOperator
    {
        IsEqual,
        IsNotEqual,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual
    }

    public class SpecialRegisters
    {
        public const int InstructionPointer = 1;
        public const int StackPointer = 2;
    }
}
