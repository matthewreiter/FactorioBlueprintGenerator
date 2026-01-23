using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Constants;
using BlueprintGenerator.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Diagnostics;
using static BlueprintGenerator.PowerUtil;

namespace BlueprintGenerator;

public class MusicBoxV2DecoderGenerator : IBlueprintGenerator
{
    public Blueprint Generate(IConfigurationRoot configuration)
    {
        return Generate(configuration.Get<MusicBoxV2DecoderConfiguration>());
    }

    public static Blueprint Generate(MusicBoxV2DecoderConfiguration configuration)
    {
        var noteGroupReferenceCount = configuration.NoteGroupReferenceCount ?? 10;
        var includePower = configuration.IncludePower ?? true;

        const int noteGroupAddressBits = 16;
        const int noteGroupTimeOffsetBits = 11;
        const int noteGroupSubAddressBits = 32 - noteGroupAddressBits - noteGroupTimeOffsetBits;

        var width = noteGroupReferenceCount;
        var mainHeight = 22;

        var noteGroupDecoderHeight = 8;

        var gridWidth = width + (includePower ? ((width + 7) / 16 + 1) * 2 : 0);
        var gridHeight = mainHeight + noteGroupDecoderHeight;
        var xOffset = -gridWidth / 2;
        var yOffset = -gridHeight / 2;

        var addressSignal = SignalID.CreateVirtual(VirtualSignalNames.Info);
        var noteGroupTimeOffsetSignal = SignalID.CreateLetterOrDigit('X');
        var noteGroupSubAddressSignal = SignalID.CreateLetterOrDigit('W');

        var entities = new List<Entity>();
        var wires = new List<Wire>();
        Entity previousAddressSuppressor = null;
        Entity previousSignalPropagator = null;
        Entity previousAddressExtractor = null;
        Entity previousNoteGroupUpdater = null;
        List<Entity> noteGroupSenders = [];
        var y = 0;

        for (int column = 0; column < width; column++)
        {
            var columnX = column + (includePower ? (column / 16 + 1) * 2 : 0) + xOffset;
            y = yOffset;
            var noteGroupReferenceSignal = SignalID.Create(MusicBoxSignals.NoteGroupReferenceSignals[column]);

            var addressSuppressor = new Entity
            {
                Player_description = "Address suppressor",
                Name = ItemNames.DeciderCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = (y += 2) - 1.5
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Decider_conditions = new DeciderConditions
                    {
                        Conditions =
                        [
                            new DeciderCondition
                            {
                                First_signal = noteGroupReferenceSignal,
                                Constant = 0,
                                Comparator = Comparators.IsNotEqual
                            }
                        ],
                        Outputs =
                        [
                            new DeciderOutput
                            {
                                Signal = SignalID.CreateVirtual(VirtualSignalNames.Check),
                                Copy_count_from_input = true
                            }
                        ]
                    }
                }
            };
            entities.Add(addressSuppressor);

            if (column > 0)
            {
                wires.Add(new((addressSuppressor, ConnectionType.Red1), (previousAddressSuppressor, ConnectionType.Red1)));
                wires.Add(new((addressSuppressor, ConnectionType.Red2), (previousAddressSuppressor, ConnectionType.Red2)));
            }


            if (column == 0)
            {
                var initialSignalPropagator = new Entity
                {
                    Player_description = "Initial signal propagator",
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = columnX - 1,
                        Y = y + 0.5
                    },
                    Direction = Direction.Down,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            Conditions =
                            [
                                new DeciderCondition
                                {
                                    First_signal = noteGroupReferenceSignal,
                                    Constant = 0,
                                    Comparator = Comparators.IsNotEqual
                                }
                            ],
                            Outputs =
                            [
                                new DeciderOutput
                                {
                                    Signal = SignalID.CreateVirtual(VirtualSignalNames.Everything),
                                    Copy_count_from_input = true
                                }
                            ]
                        }
                    }
                };
                entities.Add(initialSignalPropagator);

                previousSignalPropagator = initialSignalPropagator;
            }

            var signalPropagator = new Entity
            {
                Player_description = "Signal propagator",
                Name = ItemNames.DeciderCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = (y += 2) - 1.5
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Decider_conditions = new DeciderConditions
                    {
                        Conditions =
                        [
                            new DeciderCondition
                            {
                                First_signal = SignalID.CreateVirtual(VirtualSignalNames.Check),
                                Constant = 0,
                                Comparator = Comparators.IsEqual
                            }
                        ],
                        Outputs =
                        [
                            new DeciderOutput
                            {
                                Signal = SignalID.CreateVirtual(VirtualSignalNames.Everything),
                                Copy_count_from_input = true
                            }
                        ]
                    }
                }
            };
            entities.Add(signalPropagator);

            wires.Add(new((addressSuppressor, ConnectionType.Green1), (previousSignalPropagator, ConnectionType.Green1)));
            wires.Add(new((signalPropagator, ConnectionType.Green1), (previousSignalPropagator, ConnectionType.Green2)));

            if (column > 0)
            {
                wires.Add(new((signalPropagator, ConnectionType.Red1), (previousSignalPropagator, ConnectionType.Red1)));
            }

            var addressExtractor = new Entity
            {
                Player_description = "Address extractor",
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = (y += 2) - 1.5
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new ArithmeticConditions
                    {
                        First_signal = noteGroupReferenceSignal,
                        Second_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot),
                        Operation = ArithmeticOperations.And,
                        Output_signal = addressSignal
                    }
                }
            };
            entities.Add(addressExtractor);

            wires.Add(new((addressExtractor, ConnectionType.Green1), (previousSignalPropagator, ConnectionType.Green2)));

            if (column > 0)
            {
                wires.Add(new((addressExtractor, ConnectionType.Red1), (previousAddressExtractor, ConnectionType.Red1)));
                wires.Add(new((addressExtractor, ConnectionType.Red2), (previousAddressExtractor, ConnectionType.Red2)));
            }

            var noteGroupTimeOffsetShifter = new Entity
            {
                Player_description = "Note group time offset shifter",
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = (y += 2) - 1.5
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new ArithmeticConditions
                    {
                        First_signal = noteGroupReferenceSignal,
                        Second_constant = noteGroupAddressBits,
                        Operation = ArithmeticOperations.RightShift,
                        Output_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot)
                    }
                }
            };
            entities.Add(noteGroupTimeOffsetShifter);

            wires.Add(new((noteGroupTimeOffsetShifter, ConnectionType.Green1), (signalPropagator, ConnectionType.Green2)));

            var noteGroupTimeOffsetExtractor = new Entity
            {
                Player_description = "Note group time offset extractor",
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = (y += 2) - 1.5
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new ArithmeticConditions
                    {
                        First_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot),
                        Second_constant = (1 << noteGroupTimeOffsetBits) - 1,
                        Operation = ArithmeticOperations.And,
                        Output_signal = noteGroupTimeOffsetSignal
                    }
                }
            };
            entities.Add(noteGroupTimeOffsetExtractor);

            wires.Add(new((noteGroupTimeOffsetExtractor, ConnectionType.Red1), (noteGroupTimeOffsetShifter, ConnectionType.Red2)));

            var subAddressShifter = new Entity
            {
                Player_description = "Sub-address shifter",
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = (y += 2) - 1.5
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new ArithmeticConditions
                    {
                        First_signal = noteGroupReferenceSignal,
                        Second_constant = noteGroupAddressBits + noteGroupTimeOffsetBits,
                        Operation = ArithmeticOperations.RightShift,
                        Output_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot)
                    }
                }
            };
            entities.Add(subAddressShifter);

            wires.Add(new((subAddressShifter, ConnectionType.Green1), (noteGroupTimeOffsetShifter, ConnectionType.Green1)));

            var subAddressExtractor = new Entity
            {
                Player_description = "Sub-address extractor",
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = (y += 2) - 1.5
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new ArithmeticConditions
                    {
                        First_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot),
                        Second_constant = (1 << noteGroupSubAddressBits) - 1,
                        Operation = ArithmeticOperations.And,
                        Output_signal = noteGroupSubAddressSignal
                    }
                }
            };
            entities.Add(subAddressExtractor);

            wires.Add(new((subAddressExtractor, ConnectionType.Red1), (subAddressShifter, ConnectionType.Red2)));
            wires.Add(new((subAddressExtractor, ConnectionType.Green2), (noteGroupTimeOffsetExtractor, ConnectionType.Green2)));

            var addressChecker = new Entity
            {
                Player_description = "Checks if there is an address signal",
                Name = ItemNames.DeciderCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = (y += 2) - 1.5
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Decider_conditions = new DeciderConditions
                    {
                        Conditions =
                        [
                            new DeciderCondition
                            {
                                First_signal = addressSignal,
                                Constant = 0,
                                Comparator = Comparators.IsNotEqual
                            }
                        ],
                        Outputs =
                        [
                            new DeciderOutput
                            {
                                Signal = SignalID.CreateVirtual(VirtualSignalNames.Check)
                            }
                        ]
                    }
                }
            };
            entities.Add(addressChecker);

            wires.Add(new((addressChecker, ConnectionType.Green1), (addressExtractor, ConnectionType.Green2)));

            var noteGroupUpdater = new Entity
            {
                Player_description = "Note group updater",
                Name = ItemNames.DeciderCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = (y += 2) - 1.5
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Decider_conditions = new DeciderConditions
                    {
                        Conditions =
                        [
                            new DeciderCondition
                            {
                                First_signal = SignalID.CreateVirtual(VirtualSignalNames.Check),
                                First_signal_networks = new() { Red = true },
                                Constant = 1,
                                Comparator = Comparators.IsEqual
                            }
                        ],
                        Outputs =
                        [
                            new DeciderOutput
                            {
                                Signal = SignalID.CreateVirtual(VirtualSignalNames.Everything),
                                Copy_count_from_input = true,
                                Networks = new SignalNetworks { Green = true }
                            },
                        ]
                    }
                }
            };
            entities.Add(noteGroupUpdater);

            wires.Add(new((noteGroupUpdater, ConnectionType.Red1), (addressChecker, ConnectionType.Red2)));
            wires.Add(new((noteGroupUpdater, ConnectionType.Green2), (subAddressExtractor, ConnectionType.Green2)));

            if (column > 0)
            {
                wires.Add(new((noteGroupUpdater, ConnectionType.Green1), (previousNoteGroupUpdater, ConnectionType.Green1)));
            }

            var noteGroupMemory = new Entity
            {
                Player_description = "Note group memory",
                Name = ItemNames.DeciderCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = (y += 2) - 1.5
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Decider_conditions = new DeciderConditions
                    {
                        Conditions =
                        [
                            new DeciderCondition
                            {
                                First_signal = SignalID.CreateVirtual(VirtualSignalNames.Check),
                                First_signal_networks = new() { Red = true },
                                Constant = 0,
                                Comparator = Comparators.IsEqual
                            }
                        ],
                        Outputs =
                        [
                            new DeciderOutput
                            {
                                Signal = SignalID.CreateVirtual(VirtualSignalNames.Everything),
                                Copy_count_from_input = true,
                                Networks = new SignalNetworks { Green = true }
                            },
                        ]
                    }
                }
            };
            entities.Add(noteGroupMemory);

            wires.Add(new((noteGroupMemory, ConnectionType.Red1), (noteGroupUpdater, ConnectionType.Red1)));
            wires.Add(new((noteGroupMemory, ConnectionType.Green1), (noteGroupUpdater, ConnectionType.Green2)));
            wires.Add(new((noteGroupMemory, ConnectionType.Green1), (noteGroupMemory, ConnectionType.Green2)));

            var noteGroupSender = new Entity
            {
                Player_description = "Note group sender",
                Name = ItemNames.DeciderCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = (y += 2) - 1.5
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Decider_conditions = new DeciderConditions
                    {
                        Conditions =
                        [
                            new DeciderCondition
                            {
                                First_signal = noteGroupTimeOffsetSignal,
                                First_signal_networks = new() { Green = true },
                                Second_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot),
                                Second_signal_networks = new() { Red = true },
                                Comparator = Comparators.IsEqual
                            }
                        ],
                        Outputs =
                        [
                            new DeciderOutput
                            {
                                Signal = SignalID.CreateVirtual(VirtualSignalNames.Everything),
                                Copy_count_from_input = true,
                                Networks = new SignalNetworks { Green = true }
                            }
                        ]
                    }
                }
            };
            entities.Add(noteGroupSender);

            wires.Add(new((noteGroupSender, ConnectionType.Green1), (noteGroupMemory, ConnectionType.Green2)));

            if (column > 0)
            {
                wires.Add(new((noteGroupSender, ConnectionType.Red1), (noteGroupSenders[^1], ConnectionType.Red1)));
                wires.Add(new((noteGroupSender, ConnectionType.Green2), (noteGroupSenders[^1], ConnectionType.Green2)));
            }

            previousAddressSuppressor = addressSuppressor;
            previousSignalPropagator = signalPropagator;
            previousAddressExtractor = addressExtractor;
            previousNoteGroupUpdater = noteGroupUpdater;
            noteGroupSenders.Add(noteGroupSender);

            Debug.Assert(y == yOffset + mainHeight);
        }

        var channelCount = MusicBoxSignals.SpeakerChannelSignals.Count;
        var noteGroupCount = MusicBoxSignals.AdditionalNoteGroupSignals.Count;

        int GetColumnX(int currentColumn) => currentColumn + (includePower ? (currentColumn / 16 + 1) * 2 : 0) + xOffset;

        var column2 = 0;
        y = yOffset + mainHeight + 2;

        var allNoteSelector = new Entity
        {
            Player_description = "All note selector",
            Name = ItemNames.DeciderCombinator,
            Position = new Position
            {
                X = GetColumnX(column2++),
                Y = y + 0.5
            },
            Direction = Direction.Up,
            Control_behavior = new ControlBehavior
            {
                Decider_conditions = new DeciderConditions
                {
                    Conditions =
                    [
                        new DeciderCondition
                        {
                            First_signal = noteGroupSubAddressSignal,
                            Constant = 0,
                            Comparator = Comparators.IsEqual
                        }
                    ],
                    Outputs =
                    [
                        new DeciderOutput
                        {
                            Signal = SignalID.CreateVirtual(VirtualSignalNames.Everything),
                            Copy_count_from_input = true
                        }
                    ]
                }
            }
        };
        entities.Add(allNoteSelector);

        var allNoteBuffer = new Entity
        {
            Player_description = "All note buffer",
            Name = ItemNames.ArithmeticCombinator,
            Position = new Position
            {
                X = GetColumnX(column2++),
                Y = y + 0.5
            },
            Direction = Direction.Down,
            Control_behavior = new ControlBehavior
            {
                Arithmetic_conditions = new ArithmeticConditions
                {
                    First_signal = SignalID.CreateVirtual(VirtualSignalNames.Each),
                    Second_constant = 1,
                    Operation = ArithmeticOperations.Multiplication,
                    Output_signal = SignalID.CreateVirtual(VirtualSignalNames.Each)
                }
            }
        };
        entities.Add(allNoteBuffer);

        wires.Add(new((allNoteBuffer, ConnectionType.Green1), (noteGroupSenders[column2], ConnectionType.Green2)));
        wires.Add(new((allNoteBuffer, ConnectionType.Red2), (allNoteSelector, ConnectionType.Red1)));

        for (int noteGroupIndex = 0; noteGroupIndex < noteGroupCount; noteGroupIndex++)
        {
            var noteGroupSignals = MusicBoxSignals.AdditionalNoteGroupSignals[noteGroupIndex];
            var noteCount = noteGroupSignals.Count;
            Debug.Assert(noteCount <= channelCount);

            if (column2 + noteCount + 2 >= width)
            {
                column2 = 0;
                y += 2;
            }

            var noteGroupSelector = new Entity
            {
                Player_description = $"Note group selector {noteGroupIndex + 1}",
                Name = ItemNames.DeciderCombinator,
                Position = new Position
                {
                    X = GetColumnX(column2++),
                    Y = y + 0.5
                },
                Direction = Direction.Up,
                Control_behavior = new ControlBehavior
                {
                    Decider_conditions = new DeciderConditions
                    {
                        Conditions =
                        [
                            new DeciderCondition
                            {
                                First_signal = noteGroupSubAddressSignal,
                                Constant = noteGroupIndex + 1,
                                Comparator = Comparators.IsEqual
                            }
                        ],
                        Outputs =
                        [
                            new DeciderOutput
                            {
                                Signal = SignalID.CreateVirtual(VirtualSignalNames.Everything),
                                Copy_count_from_input = true
                            }
                        ]
                    }
                }
            };
            entities.Add(noteGroupSelector);

            var subAddressBuffer = new Entity
            {
                Player_description = $"Sub-address buffer {noteGroupIndex + 1}",
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = GetColumnX(column2++),
                    Y = y + 0.5
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new ArithmeticConditions
                    {
                        First_signal = noteGroupSubAddressSignal,
                        Second_constant = 1,
                        Operation = ArithmeticOperations.Multiplication,
                        Output_signal = noteGroupSubAddressSignal
                    }
                }
            };
            entities.Add(subAddressBuffer);

            wires.Add(new((subAddressBuffer, ConnectionType.Green1), (noteGroupSenders[column2], ConnectionType.Green2)));
            wires.Add(new((subAddressBuffer, ConnectionType.Red2), (noteGroupSelector, ConnectionType.Red1)));

            Entity previousSignalRenamer = null;

            for (int signalIndex = 0; signalIndex < noteCount; signalIndex++)
            {
                var inputSignal = SignalID.Create(noteGroupSignals[signalIndex]);
                var outputSignal = SignalID.Create(MusicBoxSignals.SpeakerChannelSignals[signalIndex]);

                var signalRenamer = new Entity
                {
                    Player_description = $"Signal renamer {noteGroupIndex + 1}-{signalIndex + 1}",
                    Name = ItemNames.ArithmeticCombinator,
                    Position = new Position
                    {
                        X = GetColumnX(column2++),
                        Y = y + 0.5
                    },
                    Direction = Direction.Down,
                    Control_behavior = new ControlBehavior
                    {
                        Arithmetic_conditions = new ArithmeticConditions
                        {
                            First_signal = inputSignal,
                            Second_constant = 1,
                            Operation = ArithmeticOperations.Multiplication,
                            Output_signal = outputSignal
                        }
                    }
                };
                entities.Add(signalRenamer);

                wires.Add(new((signalRenamer, ConnectionType.Green1), signalIndex == 0 ? (subAddressBuffer, ConnectionType.Green1) : (previousSignalRenamer, ConnectionType.Green1)));
                wires.Add(new((signalRenamer, ConnectionType.Red2), signalIndex == 0? (subAddressBuffer, ConnectionType.Red2) : (previousSignalRenamer, ConnectionType.Red2)));

                previousSignalRenamer = signalRenamer;
            }

            column2++; // Spacer
        }

        if (includePower)
        {
            var substationWidth = (width + 7) / 16 + 1;
            var substationHeight = (gridHeight + 7) / 18 + 1;

            AddSubstations(entities, wires, substationWidth, substationHeight, xOffset, yOffset);
        }

        BlueprintUtil.PopulateEntityNumbers(entities);

        return new Blueprint
        {
            Label = $"{width}x Music Box Decoder",
            Icons =
            [
                Icon.Create(ItemNames.ProgrammableSpeaker),
                Icon.Create(ItemNames.ArithmeticCombinator)
            ],
            Entities = entities,
            Wires = wires.ToArrayList()
        };
    }
}

public class MusicBoxV2DecoderConfiguration
{
    public int? NoteGroupReferenceCount { get; set; }
    public bool? IncludePower { get; set; }
}
