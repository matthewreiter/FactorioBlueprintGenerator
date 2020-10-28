using BlueprintCommon;
using BlueprintCommon.Models;
using CompilerCommon;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Reflection;

namespace Assembler
{
    public static class Assembler
    {
        public static void Run(IConfigurationRoot configuration)
        {
            Run(configuration.Get<AssemblerConfiguration>());
        }

        public static void Run(AssemblerConfiguration configuration)
        {
            var inputProgramFile = configuration.InputProgram;
            var outputBlueprintFile = configuration.OutputBlueprint;
            var outputJsonFile = configuration.OutputJson;
            var outputInstructionsFile = configuration.OutputInstructions;
            var snapToGrid = configuration.SnapToGrid;
            var x = configuration.X;
            var y = configuration.Y;
            var width = configuration.Width;
            var height = configuration.Height;

            using var instructionsWriter = new StreamWriter(outputInstructionsFile);

            var compiledProgram = AssembleCode(inputProgramFile, instructionsWriter);

            if (compiledProgram != null)
            {
                var blueprint = BlueprintGenerator.CreateBlueprintFromCompiledProgram(compiledProgram, snapToGrid, x, y, width, height, instructionsWriter);
                BlueprintUtil.PopulateIndices(blueprint);

                var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };

                BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
                BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
            }
        }

        private static CompiledProgram AssembleCode(string inputProgramFile, StreamWriter instructionsWriter)
        {
            var assembly = Assembly.LoadFrom(inputProgramFile);

            var programBuilder = new ProgramBuilder();
            programBuilder.Build(assembly, instructionsWriter);

            return new CompiledProgram
            {
                Instructions = programBuilder.Instructions,
                Data = programBuilder.Data
            };
        }
    }

    public class AssemblerConfiguration
    {
        public string InputProgram { get; set; }
        public string OutputBlueprint { get; set; }
        public string OutputJson { get; set; }
        public string OutputInstructions { get; set; }
        public bool? SnapToGrid { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }
}
