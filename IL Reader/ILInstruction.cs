// From https://www.codeproject.com/Articles/14058/Parsing-the-IL-of-a-Method-Body

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace ILReader
{
    public class ILInstruction
    {
        public OpCode Code { get; set; }

        public object Operand { get; set; }

        public int Offset { get; set; }

        /// <summary>
        /// Returns a friendly string representation of this instruction
        /// </summary>
        /// <returns></returns>
        public string GetCode()
        {
            var result = new StringBuilder();
            result.Append($"{Offset:N4}: {Code}");
            if (Operand != null)
            {
                switch (Code.OperandType)
                {
                    case OperandType.InlineField:
                        FieldInfo fOperand = (FieldInfo)Operand;
                        result.Append(" " + Globals.ProcessSpecialTypes(fOperand.FieldType.ToString()) + " " +
                            Globals.ProcessSpecialTypes(fOperand.ReflectedType.ToString()) +
                            "::" + fOperand.Name);
                        break;
                    case OperandType.InlineMethod:
                        try
                        {
                            MethodInfo mOperand = (MethodInfo)Operand;
                            result.Append(" ");
                            if (!mOperand.IsStatic)
                                result.Append("instance ");
                            result.Append(Globals.ProcessSpecialTypes(mOperand.ReturnType.ToString()) +
                                " " + Globals.ProcessSpecialTypes(mOperand.ReflectedType.ToString()) +
                                "::" + mOperand.Name + "()");
                        }
                        catch
                        {
                            try
                            {
                                ConstructorInfo mOperand = (ConstructorInfo)Operand;
                                result.Append(" ");
                                if (!mOperand.IsStatic)
                                    result.Append("instance ");
                                result.Append("void " +
                                    Globals.ProcessSpecialTypes(mOperand.ReflectedType.ToString()) +
                                    "::" + mOperand.Name + "()");
                            }
                            catch { }
                        }
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.InlineBrTarget:
                        result.Append($" {Operand:N4}");
                        break;
                    case OperandType.InlineType:
                        result.Append(" " + Globals.ProcessSpecialTypes(Operand.ToString()));
                        break;
                    case OperandType.InlineString:
                        if (Operand.ToString() == "\r\n")
                            result.Append(" \"\\r\\n\"");
                        else
                            result.Append(" \"" + Operand.ToString() + "\"");
                        break;
                    case OperandType.ShortInlineVar:
                        result.Append(Operand);
                        break;
                    case OperandType.InlineI:
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineR:
                        result.Append(Operand);
                        break;
                    case OperandType.InlineTok:
                        if (Operand is Type)
                            result.Append(((Type)Operand).FullName);
                        else
                            result.Append("not supported");
                        break;
                    default:
                        result.Append("not supported");
                        break;
                }
            }

            return result.ToString();
        }
    }
}
