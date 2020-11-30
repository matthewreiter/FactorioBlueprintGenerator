using BlueprintCommon;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System;

namespace MemoryInitializer
{
    public static class MemoryInitializer
    {
        public static void Run(IConfigurationRoot configuration)
        {
            var outputBlueprintFile = configuration["OutputBlueprint"];
            var outputJsonFile = configuration["OutputJson"];
            var memoryType = configuration["MemoryType"];

            var blueprint = memoryType switch
            {
                "ROM" => RomGenerator.Generate(configuration),
                "RAM" => RamGenerator.Generate(configuration),
                "Registers" => RegisterGenerator.Generate(configuration),
                "Speaker" => SpeakerGenerator.Generate(configuration),
                "MusicBoxSpeaker" => MusicBoxSpeakerGenerator.Generate(configuration),
                "Font" => FontGenerator.Generate(configuration),
                _ => throw new Exception($"Unsupported memory type: {memoryType}")
            };

            BlueprintUtil.PopulateIndices(blueprint);

            var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };

            BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
            BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
        }
    }
}
