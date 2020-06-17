using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using static MemoryInitializer.MemoryUtil;

namespace MemoryInitializer
{
    public static class SpeakerGenerator
    {
        public static Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<SpeakerConfiguration>());
        }

        public static Blueprint Generate(SpeakerConfiguration configuration)
        {
            const int numberOfInstruments = 12;

            var speakersPerConfiguration = configuration.SpeakersPerConfiguration ?? 6;
            var volumeLevels = configuration.VolumeLevels ?? 1;
            var baseAddress = configuration.BaseAddress ?? 0;
            var reverseAddressX = configuration.ReverseAddressX ?? false;
            var reverseAddressY = configuration.ReverseAddressY ?? false;
            var signal = configuration.Signal ?? '0';
            var includePower = configuration.IncludePower ?? true;

            var speakersPerVolumeLevel = speakersPerConfiguration * numberOfInstruments;
            var speakerCount = speakersPerVolumeLevel * volumeLevels;
            var width = configuration.Width ?? speakerCount / (configuration.Height ?? 16);
            var height = configuration.Height ?? speakerCount / width;

            const int entitiesPerCell = 6;
            const int cellHeight = 11;

            const int writerEntityOffset = -1;
            const int clearerEntityOffset = -2;
            const int readerEntityOffset = 1;
            const int playerEntityOffset = 2;
            const int speakerEntityOffset = 3;

            var gridWidth = width + (includePower ? ((width + 7) / 16 + 1) * 2 : 0);
            var gridHeight = height * cellHeight;
            var xOffset = -gridWidth / 2;
            var yOffset = -gridHeight / 2;

            var entities = new List<Entity>();

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    var relativeAddress = (reverseAddressY ? (height - row - 1) : row) * width + (reverseAddressX ? (width - column - 1) : column);
                    var address = relativeAddress + baseAddress + 1;
                    var memoryCellEntityNumber = (row * width + column) * entitiesPerCell + 2;
                    var memoryCellX = column + (includePower ? (column / 16 + 1) * 2 : 0) + xOffset;
                    var memoryCellY = (height - row - 1) * cellHeight + 4.5 + yOffset;

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
                        Direction = 4,
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
                                    Circuit_id = CircuitIds.Input
                                },
                                // Connection to reader input (address line)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + readerEntityOffset,
                                    Circuit_id = CircuitIds.Input
                                }
                            },
                            Green = new List<ConnectionData>
                            {
                                // Connection to own output (data feedback)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
                                    Circuit_id = CircuitIds.Output
                                },
                                // Connection to writer output (data in)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + writerEntityOffset,
                                    Circuit_id = CircuitIds.Output
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
                                    Circuit_id = CircuitIds.Input
                                },
                                // Connection to reader input (data out)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + readerEntityOffset,
                                    Circuit_id = CircuitIds.Input
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
                        Direction = 4,
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
                                Circuit_id = CircuitIds.Input
                            }).Concat(new List<ConnectionData>
                            {
                                // Connection to memory cell input (address line)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
                                    Circuit_id = CircuitIds.Input
                                }
                            }).ToList(),
                            // Connection to adjacent writer input (data in)
                            Green = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber + writerEntityOffset,
                                Circuit_id = CircuitIds.Input
                            }).ToList()
                        }, new ConnectionPoint
                        {
                            Green = new List<ConnectionData>
                            {
                                // Connection to memory cell input (data out)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
                                    Circuit_id = CircuitIds.Input
                                },
                                // Connection to memory cell clearer output (data in/out)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + clearerEntityOffset,
                                    Circuit_id = CircuitIds.Output
                                }
                            }
                        })
                    });

                    // Memory cell clearer
                    entities.Add(new Entity
                    {
                        Entity_number = memoryCellEntityNumber + clearerEntityOffset,
                        Name = ItemNames.ArithmeticCombinator,
                        Position = new Position
                        {
                            X = memoryCellX,
                            Y = memoryCellY - 4
                        },
                        Direction = 4,
                        Control_behavior = new ControlBehavior
                        {
                            Arithmetic_conditions = new ArithmeticConditions
                            {
                                First_signal = SignalID.CreateLetterOrDigit(signal),
                                Second_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot),
                                Operation = ArithmeticOperations.Multiplication,
                                Output_signal = SignalID.CreateLetterOrDigit(signal)
                            }
                        },
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            // Connection to adjacent clearer input (clear signal)
                            Red = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber + clearerEntityOffset,
                                Circuit_id = CircuitIds.Input
                            }).ToList(),
                            Green = new List<ConnectionData>
                            {
                                // Connection to own output (data in)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + clearerEntityOffset,
                                    Circuit_id = CircuitIds.Output
                                }
                            }
                        }, new ConnectionPoint
                        {
                            Green = new List<ConnectionData>
                            {
                                // Connection to own input (data in)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + clearerEntityOffset,
                                    Circuit_id = CircuitIds.Input
                                },
                                // Connection to memory cell writer output (data in/out)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + writerEntityOffset,
                                    Circuit_id = CircuitIds.Output
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
                        Direction = 4,
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
                                    Circuit_id = CircuitIds.Input
                                }
                            },
                            Green = new List<ConnectionData>
                            {
                                // Connection to memory cell output (data out)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber,
                                    Circuit_id = CircuitIds.Output
                                },
                                // Connection to player input (data out)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + playerEntityOffset,
                                    Circuit_id = CircuitIds.Input
                                }
                            }
                        }, new ConnectionPoint
                        {
                            // Connection to adjacent reader output (data out)
                            Green = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber + readerEntityOffset,
                                Circuit_id = CircuitIds.Output
                            }).ToList()
                        })
                    });

                    // Player
                    entities.Add(new Entity
                    {
                        Entity_number = memoryCellEntityNumber + playerEntityOffset,
                        Name = ItemNames.DeciderCombinator,
                        Position = new Position
                        {
                            X = memoryCellX,
                            Y = memoryCellY + 4
                        },
                        Direction = 4,
                        Control_behavior = new ControlBehavior
                        {
                            Decider_conditions = new DeciderConditions
                            {
                                First_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot),
                                Constant = 0,
                                Comparator = Comparators.GreaterThan,
                                Output_signal = SignalID.CreateLetterOrDigit(signal),
                                Copy_count_from_input = true
                            }
                        },
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            // Connection to adjacent player input (play signal)
                            Red = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber + playerEntityOffset,
                                Circuit_id = CircuitIds.Input
                            }).ToList(),
                            Green = new List<ConnectionData>
                            {
                                // Connection to reader input (data in)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + readerEntityOffset,
                                    Circuit_id = CircuitIds.Input
                                }
                            }
                        }, new ConnectionPoint
                        {
                            Green = new List<ConnectionData>
                            {
                                // Connection to speaker (data out)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + speakerEntityOffset
                                }
                            }
                        })
                    });

                    // Speaker
                    entities.Add(new Entity
                    {
                        Entity_number = memoryCellEntityNumber + speakerEntityOffset,
                        Name = ItemNames.ProgrammableSpeaker,
                        Position = new Position
                        {
                            X = memoryCellX,
                            Y = memoryCellY + 5.5
                        },
                        Control_behavior = new ControlBehavior
                        {
                            Circuit_condition = new CircuitCondition
                            {
                                First_signal = SignalID.CreateLetterOrDigit(signal)
                            },
                            Circuit_parameters = new CircuitParameters
                            {
                                Signal_value_is_pitch = true,
                                Instrument_id = relativeAddress % speakersPerVolumeLevel / speakersPerConfiguration
                            }
                        },
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            Green = new List<ConnectionData>
                            {
                                // Connection to player output (data in)
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + playerEntityOffset,
                                    Circuit_id = CircuitIds.Output
                                }
                            }
                        }),
                        Parameters = new SpeakerParameter
                        {
                            Playback_volume = 1 - (double)(relativeAddress / speakersPerVolumeLevel) / volumeLevels,
                            Playback_globally = true,
                            Allow_polyphony = true
                        },
                        Alert_parameters = new SpeakerAlertParameter
                        {
                            Show_alert = false
                        }
                    });
                }
            }

            if (includePower)
            {
                var substationWidth = (width + 7) / 16 + 1;
                var substationHeight = (gridHeight + 3) / 18 + 1;

                entities.AddRange(CreateSubstations(substationWidth, substationHeight, xOffset, yOffset, width * height * entitiesPerCell + 1));
            }

            return new Blueprint
            {
                Label = $"{width}x{height} Speaker",
                Icons = new List<Icon>
                {
                    new Icon
                    {
                        Signal = new SignalID
                        {
                            Type = SignalTypes.Item,
                            Name = ItemNames.ProgrammableSpeaker
                        }
                    }
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };
        }
    }

    public class SpeakerConfiguration
    {
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? SpeakersPerConfiguration { get; set; }
        public int? VolumeLevels { get; set; }
        public int? BaseAddress { get; set; }
        public bool? ReverseAddressX { get; set; }
        public bool? ReverseAddressY { get; set; }
        public char? Signal { get; set; }
        public bool? IncludePower { get; set; }
    }
}
