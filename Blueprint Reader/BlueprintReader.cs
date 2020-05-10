using BlueprintCommon;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace BlueprintReader
{
    public static class BlueprintReader
    {
        public static void Run(IConfigurationRoot configuration)
        {
            var inputBlueprintFile = configuration["InputBlueprint"];
            var outputJsonFile = configuration["OutputJson"];

            var json = BlueprintUtil.ReadBlueprintFileAsJson(inputBlueprintFile);
            var jsonObj = JsonSerializer.Deserialize<object>(json);

            BlueprintUtil.WriteOutJson(outputJsonFile, jsonObj);
        }
    }
}
