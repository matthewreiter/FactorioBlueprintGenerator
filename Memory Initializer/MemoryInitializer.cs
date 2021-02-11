using BlueprintCommon;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;

namespace MemoryInitializer
{
    public static class MemoryInitializer
    {
        public static void Run(IConfigurationRoot configuration)
        {
            var outputBlueprintFile = configuration["OutputBlueprint"];
            var outputJsonFile = configuration["OutputJson"];
            var memoryType = configuration["MemoryType"];

            var generatorType = typeof(MemoryInitializer).Assembly.GetTypes()
                .FirstOrDefault(type => type.IsAssignableTo(typeof(IBlueprintGenerator)) &&
                    type.Name.Equals($"{memoryType}Generator", StringComparison.CurrentCultureIgnoreCase));

            if (generatorType == null)
            {
                throw new Exception($"Unsupported memory type: {memoryType}");
            }

            var generator = (IBlueprintGenerator)Activator.CreateInstance(generatorType);
            var blueprint = generator.Generate(configuration);

            BlueprintUtil.PopulateIndices(blueprint);

            var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };

            BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
            BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
        }
    }
}
