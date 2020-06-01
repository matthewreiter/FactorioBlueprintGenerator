// From https://www.codeproject.com/Articles/14058/Parsing-the-IL-of-a-Method-Body

using System.Reflection;
using System.Reflection.Emit;

namespace ILReader
{
    //public enum AssemblyType
    //{
    //    None,
    //    Console,
    //    Application,
    //    Library
    //}

    //public enum BinaryOperator
    //{
    //    Add,
    //    Subtract,
    //    Multiply,
    //    Divide,
    //    Modulus,
    //    ShiftLeft,
    //    ShiftRight,
    //    IdentityEquality,
    //    IdentityInequality,
    //    ValueEquality,
    //    ValueInequality,
    //    BitwiseOr,
    //    BitwiseAnd,
    //    BitwiseExclusiveOr,
    //    BooleanOr,
    //    BooleanAnd,
    //    LessThan,
    //    LessThanOrEqual,
    //    GreaterThan,
    //    GreaterThanOrEqual
    //}

    //public enum ExceptionHandlerType
    //{
    //    Finally,
    //    Catch,
    //    Filter,
    //    Fault
    //}

    //public enum FieldVisibility
    //{
    //    Private,
    //    Public,
    //    Internal,
    //    Protected,
    //}

    //public enum MethodVisibility
    //{
    //    Private,
    //    Public,
    //    Internal,
    //    External,
    //    Protected,
    //}
    //public enum MethodModifier
    //{
    //    Static,
    //    Override,
    //    Abstract,
    //    Virtual,
    //    Final,
    //    None,
    //}


    //public enum ResourceVisibility
    //{
    //    Public,
    //    Private
    //}

    //public enum TypeVisibility
    //{
    //    vPublic,
    //    vProtected,
    //    vInternal,
    //    vProtectedInternal,
    //    vPrivate
    //}

    //public enum ClassModifiers
    //{
    //    mAbstract,
    //    mSealed,
    //    mStatic,
    //    mNone,
    //}

    //public enum UnaryOperator
    //{
    //    Negate,
    //    BooleanNot,
    //    BitwiseNot,
    //    PreIncrement,
    //    PreDecrement,
    //    PostIncrement,
    //    PostDecrement
    //}



    internal static class Globals
    {
        public static OpCode[] multiByteOpCodes;
        public static OpCode[] singleByteOpCodes;

        static Globals()
        {
            singleByteOpCodes = new OpCode[0x100];
            multiByteOpCodes = new OpCode[0x100];
            FieldInfo[] fields = typeof(OpCodes).GetFields();

            for (int index = 0; index < fields.Length; index++)
            {
                FieldInfo field = fields[index];
                if (field.FieldType == typeof(OpCode))
                {
                    OpCode opCode = (OpCode)field.GetValue(null);
                    ushort opCodeValue = (ushort)opCode.Value;
                    if (opCodeValue < 0x100)
                    {
                        singleByteOpCodes[opCodeValue] = opCode;
                    }
                    else
                    {
                        multiByteOpCodes[opCodeValue & 0xff] = opCode;
                    }
                }
            }
        }


        /// <summary>
        /// Retrieve the friendly name of a type
        /// </summary>
        /// <param name="typeName">
        /// The complete name to the type
        /// </param>
        /// <returns>
        /// The simplified name of the type (i.e. "int" instead of System.Int32)
        /// </returns>
        public static string ProcessSpecialTypes(string typeName)
        {
            string result = typeName;
            switch (typeName)
            {
                case "System.string":
                case "System.String":
                case "String":
                    result = "string"; break;
                case "System.Int32":
                case "Int":
                case "Int32":
                    result = "int"; break;
            }
            return result;
        }
    }
}
