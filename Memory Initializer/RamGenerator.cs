using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using static MemoryInitializer.ConnectionUtil;
using static MemoryInitializer.PowerUtil;

namespace MemoryInitializer
{
    public class RamGenerator : IBlueprintGenerator
    {
        public Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<RamConfiguration>());
        }

        public static Blueprint Generate(RamConfiguration configuration)
        {
            var width = configuration.Width ?? 16;
            var height = configuration.Height ?? 16;
            var baseAddress = configuration.BaseAddress ?? 0;
            var signal = configuration.Signal ?? '0';
            var includePower = configuration.IncludePower ?? true;

            const int entitiesPerCell = 3;
            const int cellHeight = 6;

            const int writerEntityOffset = -1;
            const int readerEntityOffset = 1;

            var gridWidth = width + (includePower ? ((width + 7) / 16 + 1) * 2 : 0);
            var gridHeight = height * cellHeight;
            var xOffset = -gridWidth / 2;
            var yOffset = -gridHeight / 2;

            var entities = new List<Entity>();

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    var address = row * width + column + baseAddress + 1;
                    var memoryCellEntityNumber = (row * width + column) * entitiesPerCell + 2;
                    var memoryCellX = column + (includePower ? (column / 16 + 1) * 2 : 0) + xOffset;
                    var memoryCellY = (height - row - 1) * cellHeight + 2.5 + yOffset;

                    var adjacentMemoryCells = new List<int> { -1, 1 }
                        .Where(offset => column + offset >= 0 && column + offset < width)
                        .Select(offset => memoryCellEntityNumber + offset * entitiesPerCell)
                        .Concat(new List<int> { -1, 1 }
                            .Where(offset => row + offset >= 0 && row + offset < height && column == 0)
                            .Select(offset => memoryCellEntityNumber + offset * width * entitiesPerCell)
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
                        Direction = Direction.Down,
                        Control_behavior = new ControlBehavior
                        {
                            Decider_conditions = new DeciderConditions
                            {
                                First_signal = SignalID.CreateVirtual(VirtualSignalNames.Check),
                                Constant = address,
                                Comparator = Comparators.IsNotEqual,
                                Output_signal = SignalID.CreateLetterOrDigit(signal),
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
                                    Entity_id = memoryCellEntityNumber + writerEntityOffset,
                                    Circuit_id = CircuitId.Input
                                },
                                // Connection to reader input (address line)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + readerEntityOffset,
                                    Circuit_id = CircuitId.Input
                                }
                            },
                            Green = new List<ConnectionData>
                            {
                                // Connection to own output (data feedback)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
                                    Circuit_id = CircuitId.Output
                                },
                                // Connection to writer output (data in)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + writerEntityOffset,
                                    Circuit_id = CircuitId.Output
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
                                    Circuit_id = CircuitId.Input
                                },
                                // Connection to reader input (data out)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + readerEntityOffset,
                                    Circuit_id = CircuitId.Input
                                }
                            }
                        })
                    });

                    // Memory cell writer
                    entities.Add(new Entity
                    {
                        Entity_number = memoryCellEntityNumber + writerEntityOffset,
                        Name = ItemNames.DeciderCombinator,
                        Position = new Position
                        {
                            X = memoryCellX,
                            Y = memoryCellY - 2
                        },
                        Direction = Direction.Down,
                        Control_behavior = new ControlBehavior
                        {
                            Decider_conditions = new DeciderConditions
                            {
                                First_signal = SignalID.CreateVirtual(VirtualSignalNames.Check),
                                Constant = address,
                                Comparator = Comparators.IsEqual,
                                Output_signal = SignalID.CreateLetterOrDigit(signal),
                                Copy_count_from_input = true
                            }
                        },
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            // Connection to adjacent writer input (address line)
                            Red = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber + writerEntityOffset,
                                Circuit_id = CircuitId.Input
                            }).Concat(new List<ConnectionData>
                            {
                                // Connection to memory cell input (address line)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
                                    Circuit_id = CircuitId.Input
                                }
                            }).ToList(),
                            // Connection to adjacent writer input (data in)
                            Green = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber + writerEntityOffset,
                                Circuit_id = CircuitId.Input
                            }).ToList()
                        }, new ConnectionPoint
                        {
                            Green = new List<ConnectionData>
                            {
                                // Connection to memory cell input (data out)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
                                    Circuit_id = CircuitId.Input
                                }
                            }
                        })
                    });

                    // Memory cell reader
                    entities.Add(new Entity
                    {
                        Entity_number = memoryCellEntityNumber + readerEntityOffset,
                        Name = ItemNames.DeciderCombinator,
                        Position = new Position
                        {
                            X = memoryCellX,
                            Y = memoryCellY + 2
                        },
                        Direction = Direction.Down,
                        Control_behavior = new ControlBehavior
                        {
                            Decider_conditions = new DeciderConditions
                            {
                                First_signal = SignalID.CreateVirtual(VirtualSignalNames.Info),
                                Constant = address,
                                Comparator = Comparators.IsEqual,
                                Output_signal = SignalID.CreateLetterOrDigit(signal),
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
                                    Circuit_id = CircuitId.Input
                                }
                            },
                            Green = new List<ConnectionData>
                            {
                                // Connection to memory cell output (data out)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
                                    Circuit_id = CircuitId.Output
                                }
                            }
                        }, new ConnectionPoint
                        {
                            // Connection to adjacent reader output (data out)
                            Green = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber + readerEntityOffset,
                                Circuit_id = CircuitId.Output
                            }).ToList()
                        })
                    });
                }
            }

            if (includePower)
            {
                var substationWidth = (width + 7) / 16 + 1;
                var substationHeight = (gridHeight + 3) / 18 + 1;

                entities.AddRange(CreateSubstations(substationWidth, substationHeight, xOffset, gridHeight % 18 - 4 + yOffset, width * height * entitiesPerCell + 1));
            }

            return new Blueprint
            {
                Label = $"{width}x{height} RAM",
                Icons = new List<Icon>
                {
                    new Icon
                    {
                        Signal = SignalID.Create(ItemNames.AdvancedCircuit)
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
        public char? Signal { get; set; }
        public bool? IncludePower { get; set; }
    }
}
