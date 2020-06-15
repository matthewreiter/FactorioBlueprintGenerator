using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using static MemoryInitializer.MemoryUtil;

namespace MemoryInitializer
{
    public static class RamGenerator
    {
        public static Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<RamConfiguration>());
        }

        public static Blueprint Generate(RamConfiguration configuration)
        {
            var width = configuration.Width ?? 16;
            var height = configuration.Height ?? 16;
            var baseAddress = configuration.BaseAddress ?? 0;
            var reverseAddresses = configuration.ReverseAddresses ?? false;
            var signal = configuration.Signal ?? '0';
            var includePower = configuration.IncludePower ?? true;

            var cellWidth = width + (includePower ? ((width + 7) / 16 + 1) * 2 : 0);
            var cellHeight = height * 6;
            var xOffset = -cellWidth / 2;
            var yOffset = -cellHeight / 2;

            var entities = new List<Entity>();

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    var addressOffset = row * width + column;
                    var address = (reverseAddresses ? height * width - addressOffset - 1 : addressOffset) + baseAddress + 1;
                    var memoryCellEntityNumber = (row * width + column) * 3 + 2;
                    var memoryCellX = column + (includePower ? (column / 16 + 1) * 2 : 0) + xOffset;
                    var memoryCellY = (height - row - 1) * 6 + 2.5 + yOffset;

                    var adjacentMemoryCells = new List<int> { -1, 1 }
                        .Where(offset => column + offset >= 0 && column + offset < width)
                        .Select(offset => memoryCellEntityNumber + offset * 3)
                        .Concat(new List<int> { -1, 1 }
                            .Where(offset => row + offset >= 0 && row + offset < height && column == 0)
                            .Select(offset => memoryCellEntityNumber + offset * width * 3)
                        )
                        .ToList();

                    // Memory cell
                    entities.Add(new Entity
                    {
                        Entity_number = memoryCellEntityNumber,
                        Name = ItemNames.DeciderCombinator,
                        Position = new Position
                        {
                            X = memoryCellX,
                            Y = memoryCellY
                        },
                        Direction = 4,
                        Control_behavior = new ControlBehavior
                        {
                            Decider_conditions = new DeciderConditions
                            {
                                First_signal = new SignalID
                                {
                                    Type = SignalTypes.Virtual,
                                    Name = VirtualSignalNames.Check
                                },
                                Constant = address,
                                Comparator = Comparators.IsNotEqual,
                                Output_signal = new SignalID
                                {
                                    Type = SignalTypes.Virtual,
                                    Name = VirtualSignalNames.LetterOrDigit(signal)
                                },
                                Copy_count_from_input = true
                            }
                        },
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            Red = new List<ConnectionData>
                            {
                                // Connection to writer input (address line)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber - 1,
                                    Circuit_id = 1
                                },
                                // Connection to reader input (address line)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + 1,
                                    Circuit_id = 1
                                }
                            },
                            Green = new List<ConnectionData>
                            {
                                // Connection to own output (data feedback)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
                                    Circuit_id = 2
                                },
                                // Connection to writer output (data in)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber - 1,
                                    Circuit_id = 2
                                }
                            }
                        }, new ConnectionPoint
                        {
                            Green = new List<ConnectionData>
                            {
                                // Connection to own input (data feedback)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
                                    Circuit_id = 1
                                },
                                // Connection to reader input (data out)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + 1,
                                    Circuit_id = 1
                                }
                            }
                        })
                    });

                    // Memory cell writer
                    entities.Add(new Entity
                    {
                        Entity_number = memoryCellEntityNumber - 1,
                        Name = ItemNames.DeciderCombinator,
                        Position = new Position
                        {
                            X = memoryCellX,
                            Y = memoryCellY - 2
                        },
                        Direction = 4,
                        Control_behavior = new ControlBehavior
                        {
                            Decider_conditions = new DeciderConditions
                            {
                                First_signal = new SignalID
                                {
                                    Type = SignalTypes.Virtual,
                                    Name = VirtualSignalNames.Check
                                },
                                Constant = address,
                                Comparator = Comparators.IsEqual,
                                Output_signal = new SignalID
                                {
                                    Type = SignalTypes.Virtual,
                                    Name = VirtualSignalNames.LetterOrDigit(signal)
                                },
                                Copy_count_from_input = true
                            }
                        },
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            // Connection to adjacent writer input (address line)
                            Red = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber - 1,
                                Circuit_id = 1
                            }).Concat(new List<ConnectionData>
                            {
                                // Connection to memory cell input (address line)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
                                    Circuit_id = 1
                                }
                            }).ToList(),
                            // Connection to adjacent writer input (data in)
                            Green = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber - 1,
                                Circuit_id = 1
                            }).ToList()
                        }, new ConnectionPoint
                        {
                            Green = new List<ConnectionData>
                            {
                                // Connection to memory cell input (data out)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
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
                            Y = memoryCellY + 2
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
                                    Name = VirtualSignalNames.LetterOrDigit(signal)
                                },
                                Copy_count_from_input = true
                            }
                        },
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            Red = new List<ConnectionData>
                            {
                                // Connection to memory cell input (address line)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
                                    Circuit_id = 1
                                }
                            },
                            Green = new List<ConnectionData>
                            {
                                // Connection to memory cell output (data out)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
                                    Circuit_id = 2
                                }
                            }
                        }, new ConnectionPoint
                        {
                            // Connection to adjacent reader output (data out)
                            Green = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber + 1,
                                Circuit_id = 2
                            }).ToList()
                        })
                    });
                }
            }

            if (includePower)
            {
                var substationWidth = (width + 7) / 16 + 1;
                var substationHeight = (cellHeight + 3) / 18 + 1;

                entities.AddRange(CreateSubstations(substationWidth, substationHeight, xOffset, yOffset, width * height * 3 + 1));
            }

            return new Blueprint
            {
                Label = $"{width}x{height} RAM",
                Icons = new List<Icon>
                {
                    new Icon
                    {
                        Signal = new SignalID
                        {
                            Type = SignalTypes.Item,
                            Name = ItemNames.AdvancedCircuit
                        }
                    }
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };
        }
    }

    public class RamConfiguration
    {
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? BaseAddress { get; set; }
        public bool? ReverseAddresses { get; set; }
        public char? Signal { get; set; }
        public bool? IncludePower { get; set; }
    }
}
