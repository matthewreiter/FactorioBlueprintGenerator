using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System;
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
            var memoryType = configuration["MemoryType"];

            var blueprint = memoryType switch
            {
                "ROM" => GenerateRom(configuration),
                "RAM" => GenerateRam(configuration),
                _ => throw new Exception($"Unsupported memory type: {memoryType}")
            };

            BlueprintUtil.PopulateIndices(blueprint);

            var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };

            BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
            BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
        }

        private static Blueprint GenerateRom(IConfigurationRoot configuration)
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

        private static Blueprint GenerateRam(IConfigurationRoot configuration)
        {
            var width = int.TryParse(configuration["Width"], out var widthValue) ? widthValue : 16;
            var height = int.TryParse(configuration["Height"], out var heightValue) ? heightValue : 16;
            var baseAddress = int.TryParse(configuration["BaseAddress"], out var baseAddressValue) ? baseAddressValue : 0;
            var signal = char.TryParse(configuration["Signal"], out var signalValue) ? signalValue : '0';

            var cellWidth = width + ((width + 7) / 16 + 1) * 2;
            var cellHeight = height * 6;
            var xOffset = -cellWidth / 2;
            var yOffset = -cellHeight / 2;

            var entities = new List<Entity>();

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    var address = row * width + column + baseAddress + 1;
                    var memoryCellEntityNumber = (row * width + column) * 3 + 2;
                    var memoryCellX = column + (column / 16 + 1) * 2 + xOffset;
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

            var substationWidth = (width + 7) / 16 + 1;
            var substationHeight = (cellHeight + 3) / 18 + 1;

            entities.AddRange(CreateSubstations(substationWidth, substationHeight, xOffset, yOffset, width * height * 3 + 1));

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

        private static IEnumerable<Entity> CreateSubstations(int substationWidth, int substationHeight, int xOffset, int yOffset, int baseEntityNumber)
        {
            for (int row = 0; row < substationHeight; row++)
            {
                for (int column = 0; column < substationWidth; column++)
                {
                    yield return new Entity
                    {
                        Entity_number = baseEntityNumber + row * substationWidth + column,
                        Name = ItemNames.Substation,
                        Position = new Position
                        {
                            X = column * 18 + 0.5 + xOffset,
                            Y = row * 18 + 2.5 + yOffset
                        }
                    };
                }
            }
        }

        private static Dictionary<string, ConnectionPoint> CreateConnections(params ConnectionPoint[] connectionPoints)
        {
            return connectionPoints
                .Select((connectionPoint, index) => new { Key = (index + 1).ToString(), Value = connectionPoint })
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }
}
