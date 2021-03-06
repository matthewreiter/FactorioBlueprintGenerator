﻿using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CompilerCommon
{
    public static class ProgramRomGenerator
    {
        public static Blueprint CreateBlueprintFromCompiledProgram(CompiledProgram compiledProgram, bool? snapToGrid, int? x, int? y, int? width, int? height, StreamWriter instructionsWriter)
        {
            var program = new List<MemoryCell>();
            var data = new List<MemoryCell>();

            instructionsWriter.WriteLine("Instructions:");

            var address = 1;
            foreach (var instruction in compiledProgram.Instructions)
            {
                if (instruction.OpCode != Operation.NoOp || instruction.AutoIncrement != 0 || address == compiledProgram.Instructions.Count)
                {
                    instructionsWriter.WriteLine($"{address}: {instruction.ToString(address)}");
                    program.Add(new MemoryCell { Address = address, Filters = ConvertInstructionToFilters(instruction) });
                }

                address++;
            }

            instructionsWriter.WriteLine();
            instructionsWriter.WriteLine("Data:");

            var dataAddress = 1;
            foreach (var dataCell in compiledProgram.Data)
            {
                instructionsWriter.WriteLine($"{dataAddress}: {string.Join(", ", dataCell.Select(entry => $"{entry.Key} => {entry.Value}"))}");
                data.Add(new MemoryCell { Address = dataAddress, Filters = ConvertDataToFilters(dataCell) });
                dataAddress++;
            }

            var romUsed = program.Count + data.Count;
            var totalRom = width * height;

            instructionsWriter.WriteLine();
            instructionsWriter.WriteLine($"ROM usage: {romUsed}/{totalRom} ({(double)romUsed / totalRom * 100:F1}%)");

            return RomGenerator.Generate(new RomConfiguration
            {
                SnapToGrid = snapToGrid,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                ProgramRows = height / 2,
                ProgramName = compiledProgram.Name
            }, program, data);
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

        private static List<Filter> ConvertDataToFilters(Dictionary<int, int> dataCell)
        {
            return dataCell.Select(entry => Filter.Create(SignalUtils.GetSignalByNumber(entry.Key), entry.Value)).ToList();
        }

        private static Filter CreateFilter(char signal, int count)
        {
            return Filter.Create(VirtualSignalNames.LetterOrDigit(signal), count);
        }
    }
}
