using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompilerCommon
{
    public class Instruction
    {
        private const int RamSignal = 1;

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
        public string Comment { get; set; }

        public static IEnumerable<Instruction> NoOp(int cycles) => Enumerable.Repeat(new Instruction { OpCode = Operation.NoOp }, cycles);

        public static Instruction IncrementRegister(int register, int increment, string comment = null) => new Instruction
        {
            OpCode = Operation.NoOp,
            AutoIncrement = increment,
            LeftInputRegister = register,
            Comment = comment ?? $"IncrementRegister({register}, {increment})"
        };

        public static Instruction SetRegisterToImmediateValue(int outputRegister, int immediateValue, string comment = null) =>
            SetRegister(outputRegister, immediateValue: immediateValue, comment: comment ?? $"SetRegisterToImmediateValue({outputRegister}, {immediateValue})");

        public static Instruction SetRegister(int outputRegister, int inputRegister = 0, int immediateValue = 0, int conditionLeftRegister = 0, int conditionRightImmediateValue = 0, ConditionOperator conditionOperator = ConditionOperator.IsEqual, string comment = null) => new Instruction
        {
            OpCode = Operation.Add,
            OutputRegister = outputRegister,
            LeftInputRegister = inputRegister,
            LeftImmediateValue = immediateValue,
            ConditionRegister = conditionLeftRegister,
            ConditionImmediateValue = -conditionRightImmediateValue,
            ConditionOperator = conditionOperator,
            Comment = comment ?? $"SetRegister({outputRegister}, {inputRegister}, {immediateValue}, {conditionLeftRegister}, {conditionRightImmediateValue}, {conditionOperator})"
        };

        public static Instruction Pop(int outputRegister, int additionalStackPointerAdjustment = 0, string comment = null) =>
            ReadStackValue(-1, outputRegister, stackPointerAdjustment: -1 + additionalStackPointerAdjustment, comment: comment ?? $"Pop({outputRegister}, {additionalStackPointerAdjustment})");

        public static Instruction PushImmediateValue(int value, string comment = null) => Push(immediateValue: value, comment: comment ?? $"PushImmediateValue({value})");

        public static Instruction PushRegister(int inputRegister, string comment = null) => Push(inputRegister: inputRegister, comment: comment ?? $"PushRegister({inputRegister})");

        public static Instruction Push(int inputRegister = 0, int immediateValue = 0, string comment = null) =>
            WriteMemory(addressRegister: SpecialRegisters.StackPointer, inputRegister: inputRegister, immediateValue: immediateValue, autoIncrement: 1, comment: comment ?? $"Push({inputRegister}, {immediateValue})");

        public static Instruction AdjustStackPointer(int increment, string comment = null) => IncrementRegister(SpecialRegisters.StackPointer, increment, comment: comment ?? $"AdjustStackPointer({increment})");

        public static Instruction ReadStackValue(int offset, int outputRegister, int stackPointerAdjustment = 0, string comment = null) => new Instruction
        {
            OpCode = Operation.Read,
            OutputRegister = outputRegister,
            AutoIncrement = stackPointerAdjustment,
            LeftInputRegister = SpecialRegisters.StackPointer,
            LeftImmediateValue = offset,
            RightImmediateValue = RamSignal,
            Comment = comment ?? $"ReadStackValue({offset}, {outputRegister}, {stackPointerAdjustment})"
        };

        public static Instruction WriteStackValue(int offset, int inputRegister = 0, int immediateValue = 0, string comment = null) =>
            WriteMemory(SpecialRegisters.StackPointer, offset, inputRegister, immediateValue, comment: comment ?? $"WriteStackValue({offset}, {inputRegister}, {immediateValue})");

        public static Instruction ReadMemory(int outputRegister, int addressRegister = 0, int addressValue = 0, string comment = null) =>
            ReadSignal(outputRegister, addressRegister, addressValue, signalValue: RamSignal, comment: comment ?? $"ReadMemory({outputRegister}, {addressRegister}, {addressValue})");

        public static Instruction WriteMemory(int addressRegister = 0, int addressValue = 0, int inputRegister = 0, int immediateValue = 0, int autoIncrement = 0, string comment = null) => new Instruction
        {
            OpCode = Operation.Write,
            AutoIncrement = autoIncrement,
            LeftInputRegister = addressRegister,
            LeftImmediateValue = addressValue,
            RightInputRegister = inputRegister,
            RightImmediateValue = immediateValue,
            Comment = comment ?? $"WriteMemory({addressRegister}, {addressValue}, {inputRegister}, {immediateValue}, {autoIncrement})"
        };

        public static Instruction ReadSignal(int outputRegister, int addressRegister = 0, int addressValue = 0, int signalRegister = 0, int signalValue = 0, string comment = null) => new Instruction
        {
            OpCode = Operation.Read,
            OutputRegister = outputRegister,
            LeftInputRegister = addressRegister,
            LeftImmediateValue = addressValue,
            RightInputRegister = signalRegister,
            RightImmediateValue = signalValue,
            Comment = comment ?? $"ReadSignal({outputRegister}, {addressRegister}, {addressValue}, {signalRegister}, {signalValue})"
        };

        public static Instruction BinaryOperation(Operation opCode, int outputRegister, int leftInputRegister = 0, int leftImmediateValue = 0, int rightInputRegister = 0, int rightImmediateValue = 0, string comment = null) => new Instruction
        {
            OpCode = opCode,
            OutputRegister = outputRegister,
            LeftInputRegister = leftInputRegister,
            LeftImmediateValue = leftImmediateValue,
            RightInputRegister = rightInputRegister,
            RightImmediateValue = rightImmediateValue,
            Comment = comment ?? $"BinaryOperation({opCode}, {outputRegister}, {leftInputRegister}, {leftImmediateValue}, {rightInputRegister}, {rightImmediateValue})"
        };

        public static Instruction Jump(int offset, string comment = null) =>
            IncrementRegister(SpecialRegisters.InstructionPointer, increment: offset - 2, comment: comment ?? $"Jump()"); // Account for the delay between reading the instruction and the address getting incremented

        public static Instruction JumpIf(int offset, int conditionLeftRegister = 0, int conditionRightImmediateValue = 0, ConditionOperator conditionOperator = ConditionOperator.IsEqual, string comment = null) => new Instruction
        {
            OpCode = Operation.Add,
            OutputRegister = SpecialRegisters.InstructionPointer,
            LeftInputRegister = SpecialRegisters.InstructionPointer,
            RightImmediateValue = offset - 2, // Account for the delay between reading the instruction and the address getting read in
            ConditionRegister = conditionLeftRegister,
            ConditionImmediateValue = -conditionRightImmediateValue,
            ConditionOperator = conditionOperator,
            Comment = comment ?? $"JumpIf({conditionLeftRegister}, {conditionRightImmediateValue}, {conditionOperator})"
        };

        public void SetJumpTarget(int jumpTarget)
        {
            if (OutputRegister == SpecialRegisters.InstructionPointer)
            {
                RightImmediateValue += jumpTarget;
            }
            else
            {
                AutoIncrement += jumpTarget;
            }
        }

        public string ToString(int address)
        {
            var builder = new StringBuilder();
            builder.Append(this);

            if (LeftInputRegister == SpecialRegisters.InstructionPointer && (OutputRegister == SpecialRegisters.InstructionPointer || AutoIncrement != 0))
            {
                builder.Append($" (jump to {address + AutoIncrement + RightImmediateValue + 3})");
            }

            return builder.ToString();
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append(OpCode);

            if (OutputRegister != 0)
            {
                builder.Append($" [{OutputRegister}] =");
            }

            if (LeftInputRegister != 0)
            {
                builder.Append($" [{LeftInputRegister}");

                if (AutoIncrement != 0)
                {
                    builder.Append($" += {AutoIncrement}");
                }

                builder.Append("]");
            }

            if (LeftInputRegister != 0 && LeftImmediateValue != 0)
            {
                builder.Append(" +");
            }

            if (LeftImmediateValue != 0 || LeftInputRegister == 0)
            {
                builder.Append($" {LeftImmediateValue}");
            }

            builder.Append(",");

            if (RightInputRegister != 0)
            {
                builder.Append($" [{RightInputRegister}]");
            }

            if (RightInputRegister != 0 && RightImmediateValue != 0)
            {
                builder.Append(" +");
            }

            if (RightImmediateValue != 0 || RightInputRegister == 0)
            {
                builder.Append($" {RightImmediateValue}");
            }

            if (ConditionRegister != 0 || ConditionImmediateValue != 0)
            {
                builder.Append($" if");

                if (ConditionRegister != 0)
                {
                    builder.Append($" [{ConditionRegister}]");
                }

                if (ConditionRegister != 0 && ConditionImmediateValue != 0)
                {
                    builder.Append(" +");
                }

                if (ConditionImmediateValue != 0)
                {
                    builder.Append($" {ConditionImmediateValue}");
                }

                var shortConditionOperator = ConditionOperator switch
                {
                    ConditionOperator.IsEqual => "==",
                    ConditionOperator.IsNotEqual => "!=",
                    ConditionOperator.GreaterThan => ">",
                    ConditionOperator.LessThan => "<",
                    ConditionOperator.GreaterThanOrEqual => ">=",
                    ConditionOperator.LessThanOrEqual => "<=",
                    _ => "?"
                };

                builder.Append($" {shortConditionOperator} 0");
            }

            if (Comment != null)
            {
                var spaces = 32 - builder.Length;
                if (spaces > 0)
                {
                    builder.Append(' ', spaces);
                }

                builder.Append($"// {Comment}");
            }

            return builder.ToString();
        }
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
