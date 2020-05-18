using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using static MemoryInitializer.MemoryUtil;

namespace MemoryInitializer
{
    public static class RomGenerator
    {
        public static Blueprint Generate(IConfigurationRoot configuration)
        {
            var width = int.TryParse(configuration["Width"], out var widthValue) ? widthValue : 16;
            var height = int.TryParse(configuration["Height"], out var heightValue) ? heightValue : 16;
            var programRows = int.TryParse(configuration["ProgramRows"], out var programRowsValue) ? programRowsValue : height;

            var cellWidth = width + ((width + 7) / 16 + 1) * 2;
            var cellHeight = height * 3;
            var xOffset = -cellWidth / 2;
            var yOffset = -cellHeight / 2;

            var entities = new List<Entity>();

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    var address = (row < programRows ? row : row - programRows) * width + column + 1;
                    var memoryCellEntityNumber = (row * width + column) * 2 + 1;
                    var memoryCellX = column + (column / 16 + 1) * 2 + xOffset;
                    var memoryCellY = (height - row - 1) * 3 + yOffset;

                    var adjacentMemoryCells = new List<int> { -1, 1 }
                        .Where(offset => column + offset >= 0 && column + offset < width)
                        .Select(offset => memoryCellEntityNumber + offset * 2)
                        .Concat(new List<int> { -1, 1 }
                            .Where(offset => row + offset >= 0 && row + offset < height && (row < programRows == row + offset < programRows) && column == 0)
                            .Select(offset => memoryCellEntityNumber + offset * width * 2)
                        )
                        .ToList();

                    // Memory cell
                    entities.Add(new Entity
                    {
                        Entity_number = memoryCellEntityNumber,
                        Name = ItemNames.ConstantCombinator,
                        Position = new Position
                        {
                            X = memoryCellX,
                            Y = memoryCellY
                        },
                        Direction = 4,
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            Green = new List<ConnectionData>
                            {
                                // Connection to reader input
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + 1,
                                    Circuit_id = 1
                                }
                            }
                        })
                    });

                    // Memory cell reader
                    entities.Add(new Entity
                    {
                        Entity_number = memoryCellEntityNumber + 1,
                        Name = ItemNames.DeciderCombinator,
                        Position = new Position
                        {
                            X = memoryCellX,
                            Y = memoryCellY + 1.5
                        },
                        Direction = 4,
                        Control_behavior = new ControlBehavior
                        {
                            Decider_conditions = new DeciderConditions
                            {
                                First_signal = new SignalID
                                {
                                    Type = SignalTypes.Virtual,
                                    Name = VirtualSignalNames.Info
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
                        },
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            // Connection to adjacent reader input (address line)
                            Red = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber + 1,
                                Circuit_id = 1
                            }).ToList(),
                            Green = new List<ConnectionData>
                            {
                                // Connection to memory cell
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber
                                }
                            }
                        }, new ConnectionPoint
                        {
                            // Connection to adjacent reader output
                            Green = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber + 1,
                                Circuit_id = 2
                            }).ToList()
                        })
                    });
                }
            }

            var substationWidth = (width + 7) / 16 + 1;
            var substationHeight = (cellHeight + 3) / 18 + 1;

            entities.AddRange(CreateSubstations(substationWidth, substationHeight, xOffset, yOffset, width * height * 2 + 1));

            return new Blueprint
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
        }
    }
}
