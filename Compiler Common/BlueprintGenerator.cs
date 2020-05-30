using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using MemoryInitializer;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompilerCommon
{
    public static class BlueprintGenerator
    {
        public static Blueprint CreateBlueprintFromCompiledProgram(CompiledProgram compiledProgram, int? width, int? height)
        {
            var program = new List<MemoryCell>();
            var data = new List<MemoryCell>();

            var address = 1;
            foreach (var instruction in compiledProgram.Instructions)
            {
                if (instruction.OpCode != Operation.NoOp || instruction.AutoIncrement != 0 || address == compiledProgram.Instructions.Count)
                {
                    Console.WriteLine($"{address}: {instruction.OpCode} [{instruction.OutputRegister}] + {instruction.AutoIncrement} = [{instruction.LeftInputRegister}] + {instruction.LeftImmediateValue}, [{instruction.RightInputRegister}] + {instruction.RightImmediateValue} if [{instruction.ConditionRegister}] + {instruction.ConditionImmediateValue} {instruction.ConditionOperator}");
                    program.Add(new MemoryCell { Address = address, Filters = ConvertInstructionToFilters(instruction) });
                }

                address++;
            }

            return RomGenerator.Generate(new RomConfiguration { Width = width, Height = height, ProgramRows = height / 2, ProgramName = compiledProgram.Name }, program, data);
        }

        private static List<Filter> ConvertInstructionToFilters(Instruction instruction)
        {
            return new List<Filter>
            {
                CreateFilter('0', (int)instruction.OpCode),
                CreateFilter('1', instruction.OutputRegister),
                CreateFilter('A', instruction.AutoIncrement),
                CreateFilter('2', instruction.LeftInputRegister),
                CreateFilter('B', instruction.LeftImmediateValue),
                CreateFilter('3', instruction.RightInputRegister),
                CreateFilter('C', instruction.RightImmediateValue),
                CreateFilter('4', instruction.ConditionRegister),
                CreateFilter('D', instruction.ConditionImmediateValue),
                CreateFilter('5', (int)instruction.ConditionOperator)
            }
                .Where(filter => filter.Count != 0)
                .ToList();
        }

        private static Filter CreateFilter(char signal, int count)
        {
            return new Filter { Signal = new SignalID { Name = VirtualSignalNames.LetterOrDigit(signal), Type = SignalTypes.Virtual }, Count = count };
        }
    }
}
