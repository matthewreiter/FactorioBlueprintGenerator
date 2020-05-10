using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace MemoryInitializer
{
    public static class MemoryInitializer
    {
        public static void Run(IConfigurationRoot configuration)
        {
            var outputBlueprintFile = configuration["OutputBlueprint"];
            var outputJsonFile = configuration["OutputJson"];
            var width = int.TryParse(configuration["Width"], out var widthValue) ? widthValue : 16;
            var height = int.TryParse(configuration["Height"], out var heightValue) ? heightValue : 16;

            var cellWidth = width + ((width + 7) / 16 + 1) * 2;
            var cellHeight = height * 3;
            var xOffset = -cellWidth / 2;
            var yOffset = -cellHeight / 2;

            var entities = new List<Entity>();

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    var address = row * width + column;

                    var memoryCell = new Entity
                    {
                        Entity_number = address * 2 + 1,
                        Name = ItemNames.ConstantCombinator,
                        Position = new Position
                        {
                            X = column + (column / 16 + 1) * 2 + xOffset,
                            Y = (height - row - 1) * 3 + yOffset
                        },
                        Direction = 4
                    };

                    var memoryCellReader = new Entity
                    {
                        Entity_number = memoryCell.Entity_number + 1,
                        Name = ItemNames.DeciderCombinator,
                        Position = new Position
                        {
                            X = memoryCell.Position.X,
                            Y = memoryCell.Position.Y + 2
                        },
                        Direction = 4,
                        Control_behavior = new ControlBehavior
                        {
                            Decider_conditions = new DeciderConditions
                            {
                                First_signal = new SignalID
                                {
                                    Type = SignalTypes.Virtual,
                                    Name = VirtualSignalNames.LetterOrDigit('C')
                                },
                                Constant = address,
                                Comparator = Comparators.IsEqual,
                                Output_signal = new SignalID
                                {
                                    Type = SignalTypes.Virtual,
                                    Name = VirtualSignalNames.Everything
                                },
                                Copy_count_from_input = true
                            }
                        }
                    };

                    memoryCell.Connections = CreateConnections(new ConnectionPoint
                    {
                        Green = new List<ConnectionData>
                        {
                            new ConnectionData
                            {
                                Entity_id = memoryCellReader.Entity_number,
                                Circuit_id = 1
                            }
                        }
                    });

                    memoryCellReader.Connections = CreateConnections(new ConnectionPoint
                    {
                        Green = new List<ConnectionData>
                        {
                            new ConnectionData
                            {
                                Entity_id = memoryCell.Entity_number
                            }
                        }
                    });

                    entities.Add(memoryCell);
                    entities.Add(memoryCellReader);
                }
            }

            var blueprint = new Blueprint
            {
                Label = $"{width}x{height} ROM",
                Icons = new List<Icon>
                {
                    new Icon
                    {
                        Signal = new SignalID
                        {
                            Type = SignalTypes.Item,
                            Name = ItemNames.ElectronicCircuit
                        }
                    }
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };

            BlueprintUtil.PopulateIndices(blueprint);

            var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };

            BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
            BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
        }

        private static Dictionary<string, ConnectionPoint> CreateConnections(params ConnectionPoint[] connectionPoints)
        {
            return connectionPoints
                .Select((connectionPoint, index) => new { Key = (index + 1).ToString(), Value = connectionPoint })
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }
}
