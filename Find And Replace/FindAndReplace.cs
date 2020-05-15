using BlueprintCommon;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FindAndReplace
{
    public static class FindAndReplace
    {
        public static void Run(IConfigurationRoot configuration)
        {
            var inputBlueprintFile = configuration["InputBlueprint"];
            var outputBlueprintFile = configuration["OutputBlueprint"];
            var outputJsonFile = configuration["OutputJson"];
            var outputUpdatedJsonFile = configuration["OutputUpdatedJson"];
            var signalToFind = configuration["SignalToFind"];
            var replacementSignal = configuration["ReplacementSignal"];

            var json = BlueprintUtil.ReadBlueprintFileAsJson(inputBlueprintFile);
            var jsonObj = JsonSerializer.Deserialize<object>(json);
            var blueprintWrapper = BlueprintUtil.DeserializeBlueprintWrapper(json);

            BlueprintUtil.WriteOutJson(outputJsonFile, jsonObj);

            foreach (var entity in blueprintWrapper.Blueprint.Entities)
            {
                var signals = new List<SignalID>
                {
                    entity.Control_behavior?.Circuit_condition?.First_signal,
                    entity.Control_behavior?.Circuit_condition?.Second_signal,
                    entity.Control_behavior?.Arithmetic_conditions?.First_signal,
                    entity.Control_behavior?.Arithmetic_conditions?.Second_signal,
                    entity.Control_behavior?.Arithmetic_conditions?.Output_signal,
                    entity.Control_behavior?.Decider_conditions?.First_signal,
                    entity.Control_behavior?.Decider_conditions?.Second_signal,
                    entity.Control_behavior?.Decider_conditions?.Output_signal
                }
                    .Concat(entity.Control_behavior?.Filters?.Select(filter => filter.Signal) ?? new List<SignalID>())
                    .Where(signal => signal != null);

                foreach (var signal in signals)
                {
                    if (signal.Name == signalToFind)
                    {
                        signal.Name = replacementSignal;
                    }
                }
            }

            BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
            BlueprintUtil.WriteOutJson(outputUpdatedJsonFile, blueprintWrapper);
        }
    }
}
