using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using static MemoryInitializer.ConnectionUtil;

namespace MemoryInitializer.Screen
{
    public class PixelSignalsGenerator : IBlueprintGenerator
    {
        public Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<PixelSignalsConfiguration>());
        }

        public static Blueprint Generate(PixelSignalsConfiguration configuration)
        {
            var signalCount = configuration.SignalCount ?? ScreenUtil.PixelSignals.Count;

            const int maxFilters = 20;

            var entities = new List<Entity>();
            var signalConstants = new Entity[(signalCount + maxFilters - 1) / maxFilters];

            // Signal constants
            for (var index = 0; index < signalConstants.Length; index++)
            {
                var outputSignalMap = new Entity
                {
                    Name = ItemNames.ConstantCombinator,
                    Position = new Position
                    {
                        X = index + 1,
                        Y = 0
                    },
                    Direction = Direction.Right,
                    Control_behavior = new ControlBehavior
                    {
                        Filters = ScreenUtil.PixelSignals.Skip(index * maxFilters).Take(Math.Min(maxFilters, signalCount - index * maxFilters)).Select((signal, signalIndex) => new Filter
                        {
                            Signal = SignalID.Create(signal),
                            Count = 1
                        }).ToList()
                    }
                };
                signalConstants[index] = outputSignalMap;
                entities.Add(outputSignalMap);
            }

            BlueprintUtil.PopulateEntityNumbers(entities);

            // Signal constant connections
            for (var index = 1; index < signalConstants.Length; index++)
            {
                var outputSignalMap = signalConstants[index];
                var adjacentOutputSignalMap = signalConstants[index - 1];

                AddConnection(CircuitColor.Red, outputSignalMap, null, adjacentOutputSignalMap, null);
            }

            return new Blueprint
            {
                Label = $"Pixel signals",
                Icons = new List<Icon>
                {
                    new Icon
                    {
                        Signal = SignalID.Create(ItemNames.Lamp)
                    },
                    new Icon
                    {
                        Signal = SignalID.Create(ItemNames.ConstantCombinator)
                    }
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };
        }
    }

    public class PixelSignalsConfiguration
    {
        public int? SignalCount { get; init; }
    }
}
