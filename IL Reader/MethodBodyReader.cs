// From https://www.codeproject.com/Articles/14058/Parsing-the-IL-of-a-Method-Body

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace ILReader
{
    public static class MethodBodyReader
    {
        /// <summary>
        /// Constructs the array of ILInstructions according to the IL byte code.
        /// </summary>
        /// <param name="module"></param>
        public static List<ILInstruction> GetInstructions(this MethodBase method)
        {
            var methodBody = method.GetMethodBody();

            if (methodBody == null)
            {
                return new List<ILInstruction> { };
            }

            var il = methodBody.GetILAsByteArray();
            var module = method.Module;
            var instructions = new List<ILInstruction>();
            int position = 0;

            while (position < il.Length)
            {
                ILInstruction instruction = new ILInstruction();

                // get the operation code of the current instruction
                OpCode code;
                ushort value = il[position++];
                if (value != 0xfe)
                {
                    code = Globals.singleByteOpCodes[value];
                }
                else
                {
                    value = il[position++];
                    code = Globals.multiByteOpCodes[value];
                }
                instruction.Code = code;
                instruction.Offset = position - 1;
                int metadataToken;
                // get the operand of the current operation
                switch (code.OperandType)
                {
                    case OperandType.InlineBrTarget:
                        metadataToken = ReadInt32(il, ref position);
                        metadataToken += position;
                        instruction.Operand = metadataToken;
                        break;
                    case OperandType.InlineField:
                        metadataToken = ReadInt32(il, ref position);
                        instruction.Operand = module.ResolveField(metadataToken, method.DeclaringType.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                        break;
                    case OperandType.InlineMethod:
                        metadataToken = ReadInt32(il, ref position);
                        try
                        {
                            instruction.Operand = module.ResolveMethod(metadataToken, method.DeclaringType.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                        }
                        catch
                        {
                            instruction.Operand = module.ResolveMember(metadataToken, method.DeclaringType.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                        }
                        break;
                    case OperandType.InlineSig:
                        metadataToken = ReadInt32(il, ref position);
                        instruction.Operand = module.ResolveSignature(metadataToken);
                        break;
                    case OperandType.InlineTok:
                        metadataToken = ReadInt32(il, ref position);
                        var member = module.ResolveMember(metadataToken, method.DeclaringType.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);

                        if (member.MemberType == MemberTypes.TypeInfo)
                        {
                            instruction.Operand = module.ResolveType(metadataToken, method.DeclaringType.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                        }
                        else
                        {
                            instruction.Operand = member;
                        }
                        break;
                    case OperandType.InlineType:
                        metadataToken = ReadInt32(il, ref position);
                        instruction.Operand = module.ResolveType(metadataToken, method.DeclaringType.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                        break;
                    case OperandType.InlineI:
                        {
                            instruction.Operand = ReadInt32(il, ref position);
                            break;
                        }
                    case OperandType.InlineI8:
                        {
                            instruction.Operand = ReadInt64(il, ref position);
                            break;
                        }
                    case OperandType.InlineNone:
                        {
                            instruction.Operand = null;
                            break;
                        }
                    case OperandType.InlineR:
                        {
                            instruction.Operand = ReadDouble(il, ref position);
                            break;
                        }
                    case OperandType.InlineString:
                        {
                            metadataToken = ReadInt32(il, ref position);
                            instruction.Operand = module.ResolveString(metadataToken);
                            break;
                        }
                    case OperandType.InlineSwitch:
                        {
                            int count = ReadInt32(il, ref position);
                            int[] casesAddresses = new int[count];
                            for (int i = 0; i < count; i++)
                            {
                                casesAddresses[i] = ReadInt32(il, ref position);
                            }
                            int[] cases = new int[count];
                            for (int i = 0; i < count; i++)
                            {
                                cases[i] = position + casesAddresses[i];
                            }
                            break;
                        }
                    case OperandType.InlineVar:
                        {
                            instruction.Operand = ReadUInt16(il, ref position);
                            break;
                        }
                    case OperandType.ShortInlineBrTarget:
                        {
                            instruction.Operand = ReadSByte(il, ref position) + position;
                            break;
                        }
                    case OperandType.ShortInlineI:
                        {
                            instruction.Operand = ReadSByte(il, ref position);
                            break;
                        }
                    case OperandType.ShortInlineR:
                        {
                            instruction.Operand = ReadSingle(il, ref position);
                            break;
                        }
                    case OperandType.ShortInlineVar:
                        {
                            instruction.Operand = ReadByte(il, ref position);
                            break;
                        }
                    default:
                        {
                            throw new Exception("Unknown operand type.");
                        }
                }
                instructions.Add(instruction);
            }

            return instructions;
        }

        #region IL read methods
        private static int ReadInt16(byte[] il, ref int position)
        {
            return il[position++] | (il[position++] << 8);
        }
        private static ushort ReadUInt16(byte[] il, ref int position)
        {
            return (ushort)(il[position++] | (il[position++] << 8));
        }
        private static int ReadInt32(byte[] il, ref int position)
        {
            return il[position++] | (il[position++] << 8) | (il[position++] << 0x10) | (il[position++] << 0x18);
        }
        private static ulong ReadInt64(byte[] il, ref int position)
        {
            return il[position++] | ((ulong)il[position++] << 8) | ((ulong)il[position++] << 0x10) | ((ulong)il[position++] << 0x18) | ((ulong)il[position++] << 0x20) | ((ulong)il[position++] << 0x28) | ((ulong)il[position++] << 0x30) | ((ulong)il[position++] << 0x38);
        }
        private static double ReadDouble(byte[] il, ref int position)
        {
            return il[position++] | ((ulong)il[position++] << 8) | ((ulong)il[position++] << 0x10) | ((ulong)il[position++] << 0x18) | ((ulong)il[position++] << 0x20) | ((ulong)il[position++] << 0x28) | ((ulong)il[position++] << 0x30) | ((ulong)il[position++] << 0x38);
        }
        private static sbyte ReadSByte(byte[] il, ref int position)
        {
            return (sbyte)il[position++];
        }
        private static byte ReadByte(byte[] il, ref int position)
        {
            return il[position++];
        }
        private static float ReadSingle(byte[] il, ref int position)
        {
            return il[position++] | (il[position++] << 8) | (il[position++] << 0x10) | (il[position++] << 0x18);
        }

        #endregion
    }
}
