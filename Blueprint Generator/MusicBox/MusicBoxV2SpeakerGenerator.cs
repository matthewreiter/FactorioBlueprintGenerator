using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Constants;
using BlueprintGenerator.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static BlueprintGenerator.PowerUtil;

namespace BlueprintGenerator.MusicBox;

public class MusicBoxV2SpeakerGenerator : IBlueprintGenerator
{
    public Blueprint Generate(IConfigurationRoot configuration)
    {
        return Generate(configuration.Get<MusicBoxV2SpeakerConfiguration>());
    }

    public static Blueprint Generate(MusicBoxV2SpeakerConfiguration configuration)
    {
        var instrumentCount = configuration.InstrumentCount ?? 10;
        var channelCount = configuration.ChannelCount ?? 10;
        var includePower = configuration.IncludePower ?? true;
        var isHorizontal = configuration.IsHorizontal ?? false;

        const int maxInstruments = 10;
        const int maxPitches = 48;
        const int maxVolumes = 100;
        const int ticksPerCycle = 3;
        const int trailOffTicks = 10;
        const int pitchesPerGroup = 6; // 2 groups per octave for a total of 12 groups
        const int octaveCount = 6;
        const int notesPerOctave = 12;

        const int pitchGroupCount = octaveCount * notesPerOctave / pitchesPerGroup;

        const int headerHeight = 42;
        const int speakerCellHeight = 3;
        const int footerHeight = 16;
        const int displayCellHeight = 4;

        var gridWidth = channelCount + (includePower ? ((channelCount + 7) / 16 + 1) * 2 : 0);
        var gridHeight = headerHeight + instrumentCount * (speakerCellHeight + displayCellHeight) + footerHeight;
        var xOffset = -gridWidth / 2;
        var yOffset = -gridHeight / 2;

        var durationSignal = SignalID.CreateVirtual(VirtualSignalNames.Wait);
        var elapsedTimeSignal = SignalID.CreateVirtual(VirtualSignalNames.Clock);
        var remainingTimeSignal = SignalID.CreateVirtual(VirtualSignalNames.Sun);
        var modularTimeSignal = SignalID.CreateVirtual(VirtualSignalNames.Speed);
        var instrumentSignal = SignalID.CreateVirtual(VirtualSignalNames.Snowflake);
        var pitchSignal = SignalID.CreateVirtual(VirtualSignalNames.Fire);
        var timeGatedPitchSignal = SignalID.CreateVirtual(VirtualSignalNames.Explosion);
        var pitchGroupSignal = SignalID.CreateVirtual(VirtualSignalNames.Liquid);
        var volumeSignal = SignalID.CreateVirtual(VirtualSignalNames.Alarm);
        var volumeAdjustmentSignal = SignalID.CreateVirtual(VirtualSignalNames.Moon);
        var masterVolumeSignal = SignalID.CreateVirtual(VirtualSignalNames.Sun);
        var resetSignal = SignalID.CreateVirtual(VirtualSignalNames.Deny);

        (int VolumeAdjustment, string Description, List<DeciderCondition> Conditions)[] volumeAdjustments =
        [
            (100, "First repetition volume adjustment",
            [
                new()
                {
                    First_signal = elapsedTimeSignal,
                    Constant = 0,
                    Comparator = Comparators.GreaterThan
                },
                new()
                {
                    First_signal = elapsedTimeSignal,
                    Constant = 5,
                    Comparator = Comparators.LessThan,
                    Compare_type = CompareTypes.And
                }
            ]),
            (80, "Second repetition volume adjustment",
            [
                new()
                {
                    First_signal = elapsedTimeSignal,
                    Constant = 5,
                    Comparator = Comparators.GreaterThanOrEqualTo
                },
                new()
                {
                    First_signal = elapsedTimeSignal,
                    Constant = 9,
                    Comparator = Comparators.LessThan,
                    Compare_type = CompareTypes.And
                }
            ]),
            (30, "Sustained note volume adjustment",
            [
                new()
                {
                    First_signal = elapsedTimeSignal,
                    Constant = 9,
                    Comparator = Comparators.GreaterThanOrEqualTo
                }
            ])
        ];

        (Instrument[] Instruments, int Offset)[] instrumentOffsets =
        [
            ([Instrument.Piano, Instrument.SteelDrum], 12),
            ([Instrument.PluckedStrings], 24),
            ([Instrument.Celesta, Instrument.Vibraphone], 36)
        ];

        var entities = new List<Entity>();
        var wires = new List<Wire>();
        Entity previousDurationDivider = null;
        Entity previousInstrumentDivider = null;
        Entity previousCurrentNoteMemory = null;
        List<Entity> previousVolumeAdjustmentPickers = null;
        Entity previousMasterVolumeMultiplier = null;
        Entity previousPitchGrouper = null;
        List<Entity> previousInstrumentOffsetPickers = null;
        Entity previousPitchGroupMapper = null;
        Entity previousGroupVolumeMultiplier = null;
        Entity previousFirstPitchMapper = null;
        List<Entity> previousDisplayVolumeMultipliers = null;

        var inputBuffer = new Entity
        {
            Player_description = "Input buffer",
            Name = ItemNames.ArithmeticCombinator,
            Position = new Position
            {
                X = (includePower ? 2 : 0) + xOffset - 1,
                Y = yOffset + 0.5
            },
            Direction = Direction.Up,
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
        entities.Add(inputBuffer);

        var resetter = new Entity
        {
            Player_description = "Resetter",
            Name = ItemNames.DeciderCombinator,
            Position = new Position
            {
                X = (includePower ? 2 : 0) + xOffset - 1,
                Y = yOffset + 4.5
            },
            Direction = Direction.Down,
            Control_behavior = new ControlBehavior
            {
                Decider_conditions = new DeciderConditions
                {
                    Conditions =
                    [
                        new()
                        {
                            First_signal = SignalID.CreateVirtual(VirtualSignalNames.Check),
                            Constant = 34817,
                            Comparator = Comparators.IsEqual
                        }
                    ],
                    Outputs =
                    [
                        new()
                        {
                            Signal = resetSignal
                        }
                    ]
                }
            }
        };
        entities.Add(resetter);

        var volumeAdjustmentProviders = new List<Entity>();
        foreach (var ((volumeAdjustment, description, conditions), volumeLevelIndex) in volumeAdjustments.Select((volumeAdjustment, index) => (volumeAdjustment, index)))
        {
            var volumeAdjustmentProvider = new Entity
            {
                Player_description = description,
                Name = ItemNames.ConstantCombinator,
                Position = new Position
                {
                    X = (includePower ? 2 : 0) + xOffset - 1,
                    Y = yOffset + 28 + volumeLevelIndex * 2
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Sections = Sections.Create([Filter.Create(volumeAdjustmentSignal, volumeAdjustment)])
                }
            };
            entities.Add(volumeAdjustmentProvider);
            volumeAdjustmentProviders.Add(volumeAdjustmentProvider);
        }

        var masterVolumeProvider = new Entity
        {
            Player_description = "Master volume provider",
            Name = ItemNames.ConstantCombinator,
            Position = new Position
            {
                X = (includePower ? 2 : 0) + xOffset - 1,
                Y = yOffset + 30 + volumeAdjustments.Length * 2
            },
            Direction = Direction.Down,
            Control_behavior = new ControlBehavior
            {
                Sections = Sections.Create([Filter.Create(masterVolumeSignal, 100)])
            }
        };
        entities.Add(masterVolumeProvider);

        var pitchOffsetProvider = new Entity
        {
            Player_description = "Pitch offset provider",
            Name = ItemNames.ConstantCombinator,
            Position = new Position
            {
                X = (includePower ? 2 : 0) + xOffset - 1,
                Y = yOffset + headerHeight + instrumentCount * speakerCellHeight + 1
            },
            Direction = Direction.Down,
            Control_behavior = new ControlBehavior
            {
                Sections = Sections.Create([Filter.Create(pitchSignal, pitchesPerGroup - 1)])
            }
        };
        entities.Add(pitchOffsetProvider);

        var instrumentOffsetProviders = new List<Entity>();
        foreach (var ((instruments, offset), instrumentOffsetIndex) in instrumentOffsets.Select((instrumentOffset, index) => (instrumentOffset, index)))
        {
            var instrumentOffsetProvider = new Entity
            {
                Player_description = $"Instrument pitch group offset provider for {string.Join(", ", instruments)}",
                Name = ItemNames.ConstantCombinator,
                Position = new Position
                {
                    X = (includePower ? 2 : 0) + xOffset - 1,
                    Y = yOffset + headerHeight + instrumentCount * speakerCellHeight + 4 + instrumentOffsetIndex * 2
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Sections = Sections.Create([Filter.Create(pitchGroupSignal, offset / pitchesPerGroup)])
                }
            };
            entities.Add(instrumentOffsetProvider);
            instrumentOffsetProviders.Add(instrumentOffsetProvider);
        }

        var pitchGroupMappingProvider = new Entity
        {
            Player_description = "Pitch group mapping provider",
            Name = ItemNames.ConstantCombinator,
            Position = new Position
            {
                X = (includePower ? 2 : 0) + xOffset - 1,
                Y = yOffset + headerHeight + instrumentCount * speakerCellHeight + 6 + instrumentOffsets.Length * 2
            },
            Direction = Direction.Down,
            Control_behavior = new ControlBehavior
            {
                Sections = Sections.Create([.. Enumerable.Range(0, pitchGroupCount).Select(pitchGroup => Filter.Create(SignalID.CreateLetterOrDigit((char)('A' + pitchGroup)), pitchGroup + 1))])
            }
        };
        entities.Add(pitchGroupMappingProvider);

        var pitchMappingProvider = new Entity
        {
            Player_description = "Pitch mapping provider",
            Name = ItemNames.ConstantCombinator,
            Position = new Position
            {
                X = (includePower ? 2 : 0) + xOffset - 1,
                Y = yOffset + headerHeight + instrumentCount * speakerCellHeight + 10 + instrumentOffsets.Length * 2
            },
            Direction = Direction.Down,
            Control_behavior = new ControlBehavior
            {
                Sections = Sections.Create([.. MusicBoxSignals.NoteDisplaySignals.Select((signalName, index) => Filter.Create(signalName, index + 1))])
            }
        };
        entities.Add(pitchMappingProvider);

        for (int voiceIndex = 0; voiceIndex < channelCount; voiceIndex++)
        {
            var columnX = voiceIndex + (includePower ? (voiceIndex / 16 + 1) * 2 : 0) + xOffset;
            var y = yOffset;
            var inputSignal = SignalID.Create(MusicBoxSignals.SpeakerChannelSignals[voiceIndex]);
            var players = new List<Entity>();

            var durationDivider = new Entity
            {
                Player_description = $"Duration divider for voice {voiceIndex + 1}",
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
                        First_signal = inputSignal,
                        Second_constant = maxInstruments * maxPitches * maxVolumes,
                        Operation = ArithmeticOperations.Division,
                        Output_signal = durationSignal
                    }
                }
            };
            entities.Add(durationDivider);

            wires.Add(new((durationDivider, ConnectionType.Red1), voiceIndex == 0 ? (inputBuffer, ConnectionType.Red2) : (previousDurationDivider, ConnectionType.Red1)));

            previousDurationDivider = durationDivider;

            var instrumentDivider = new Entity
            {
                Player_description = $"Instrument divider for voice {voiceIndex + 1}",
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
                        First_signal = inputSignal,
                        Second_constant = maxPitches * maxVolumes,
                        Operation = ArithmeticOperations.Division,
                        Output_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot)
                    }
                }
            };
            entities.Add(instrumentDivider);

            wires.Add(new((instrumentDivider, ConnectionType.Green1), voiceIndex == 0 ? (inputBuffer, ConnectionType.Green1) : (previousInstrumentDivider, ConnectionType.Green1)));

            previousInstrumentDivider = instrumentDivider;

            var instrumentExtractor = new Entity
            {
                Player_description = $"Instrument extractor for voice {voiceIndex + 1}",
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
                        Second_constant = maxInstruments,
                        Operation = ArithmeticOperations.Modulus,
                        Output_signal = instrumentSignal
                    }
                }
            };
            entities.Add(instrumentExtractor);

            wires.Add(new((instrumentExtractor, ConnectionType.Red1), (instrumentDivider, ConnectionType.Red2)));
            wires.Add(new((instrumentExtractor, ConnectionType.Red2), (durationDivider, ConnectionType.Red2)));

            var pitchDivider = new Entity
            {
                Player_description = $"Pitch divider for voice {voiceIndex + 1}",
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
                        First_signal = inputSignal,
                        Second_constant = maxVolumes,
                        Operation = ArithmeticOperations.Division,
                        Output_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot)
                    }
                }
            };
            entities.Add(pitchDivider);

            wires.Add(new((pitchDivider, ConnectionType.Green1), (instrumentDivider, ConnectionType.Green1)));

            var pitchExtractor = new Entity
            {
                Player_description = $"Pitch extractor for voice {voiceIndex + 1}",
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
                        Second_constant = maxPitches,
                        Operation = ArithmeticOperations.Modulus,
                        Output_signal = pitchSignal
                    }
                }
            };
            entities.Add(pitchExtractor);

            wires.Add(new((pitchExtractor, ConnectionType.Red1), (pitchDivider, ConnectionType.Red2)));
            wires.Add(new((pitchExtractor, ConnectionType.Red2), (instrumentExtractor, ConnectionType.Red2)));

            var volumeExtractor = new Entity
            {
                Player_description = $"Volume extractor for voice {voiceIndex + 1}",
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
                        First_signal = inputSignal,
                        Second_constant = maxVolumes,
                        Operation = ArithmeticOperations.Modulus,
                        Output_signal = volumeSignal
                    }
                }
            };
            entities.Add(volumeExtractor);

            wires.Add(new((volumeExtractor, ConnectionType.Red1), (durationDivider, ConnectionType.Red1)));
            wires.Add(new((volumeExtractor, ConnectionType.Red2), (pitchExtractor, ConnectionType.Red2)));

            var elapsedTimeIncrementer = new Entity
            {
                Player_description = $"Elapsed time incrementer for voice {voiceIndex + 1}",
                Name = ItemNames.ConstantCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = y++
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Sections = Sections.Create([Filter.Create(elapsedTimeSignal, 1)])
                },
            };
            entities.Add(elapsedTimeIncrementer);

            wires.Add(new((elapsedTimeIncrementer, ConnectionType.Red1), (volumeExtractor, ConnectionType.Red2)));

            var currentNoteMemory = new Entity
            {
                Player_description = $"Current note memory for voice {voiceIndex + 1}",
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
                            new()
                            {
                                First_signal = elapsedTimeSignal,
                                Second_signal = durationSignal,
                                Comparator = Comparators.LessThanOrEqualTo
                            },
                            new()
                            {
                                First_signal = resetSignal,
                                Constant = 0,
                                Comparator = Comparators.IsEqual,
                                Compare_type = CompareTypes.And
                            }
                        ],
                        Outputs =
                        [
                            new()
                            {
                                Signal = SignalID.CreateVirtual(VirtualSignalNames.Everything),
                                Copy_count_from_input = true
                            }
                        ]
                    }
                }
            };
            entities.Add(currentNoteMemory);

            wires.Add(new((currentNoteMemory, ConnectionType.Red1), (currentNoteMemory, ConnectionType.Red2)));
            wires.Add(new((currentNoteMemory, ConnectionType.Red1), (elapsedTimeIncrementer, ConnectionType.Red1)));
            wires.Add(new((currentNoteMemory, ConnectionType.Green1), voiceIndex == 0 ? (resetter, ConnectionType.Green2) : (previousCurrentNoteMemory, ConnectionType.Green1)));

            previousCurrentNoteMemory = currentNoteMemory;

            var timeDivider = new Entity
            {
                Player_description = $"Time divider for voice {voiceIndex + 1}",
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
                        First_signal = elapsedTimeSignal,
                        Second_constant = ticksPerCycle,
                        Operation = ArithmeticOperations.Modulus,
                        Output_signal = modularTimeSignal
                    }
                }
            };
            entities.Add(timeDivider);

            wires.Add(new((timeDivider, ConnectionType.Green1), (currentNoteMemory, ConnectionType.Green2)));
            wires.Add(new((timeDivider, ConnectionType.Green2), (timeDivider, ConnectionType.Green1)));

            var remainingTimeCalculator = new Entity
            {
                Player_description = $"Remaining time calculator for voice {voiceIndex + 1}",
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
                        First_signal = durationSignal,
                        Second_signal = elapsedTimeSignal,
                        Operation = ArithmeticOperations.Subtraction,
                        Output_signal = remainingTimeSignal
                    }
                }
            };
            entities.Add(remainingTimeCalculator);

            wires.Add(new((remainingTimeCalculator, ConnectionType.Green1), (timeDivider, ConnectionType.Green2)));
            wires.Add(new((remainingTimeCalculator, ConnectionType.Green2), (remainingTimeCalculator, ConnectionType.Green1)));

            var offsetter = new Entity
            {
                Player_description = $"Offsetter for voice {voiceIndex + 1}",
                Name = ItemNames.ConstantCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = y++
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Sections = Sections.Create([Filter.Create(pitchSignal, 1), Filter.Create(volumeSignal, 1), Filter.Create(instrumentSignal, 1)])
                }
            };
            entities.Add(offsetter);

            wires.Add(new((offsetter, ConnectionType.Green1), (remainingTimeCalculator, ConnectionType.Green2)));

            var signalBuffer = new Entity
            {
                Player_description = $"Signal buffer for voice {voiceIndex + 1}",
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
                        First_signal = SignalID.CreateVirtual(VirtualSignalNames.Each),
                        Second_constant = 1,
                        Operation = ArithmeticOperations.Multiplication,
                        Output_signal = SignalID.CreateVirtual(VirtualSignalNames.Each)
                    }
                }
            };
            entities.Add(signalBuffer);

            wires.Add(new((signalBuffer, ConnectionType.Green1), (offsetter, ConnectionType.Green1)));

            var pitchRenamer = new Entity
            {
                Player_description = $"Pitch renamer for voice {voiceIndex + 1}",
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
                        First_signal = pitchSignal,
                        Second_constant = 1,
                        Operation = ArithmeticOperations.Multiplication,
                        Output_signal = timeGatedPitchSignal,
                    }
                }
            };
            entities.Add(pitchRenamer);

            wires.Add(new((pitchRenamer, ConnectionType.Green1), (signalBuffer, ConnectionType.Green1)));

            var timeGate = new Entity
            {
                Player_description = $"Time gate for voice {voiceIndex + 1}",
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
                            new()
                            {
                                First_signal = elapsedTimeSignal,
                                First_signal_networks = new() { Green = true },
                                Constant = 1,
                                Comparator = Comparators.IsEqual
                            },
                            new()
                            {
                                First_signal = modularTimeSignal,
                                First_signal_networks = new() { Green = true },
                                Constant = 1,
                                Comparator = Comparators.IsEqual,
                                Compare_type = CompareTypes.Or
                            },
                            new()
                            {
                                First_signal = remainingTimeSignal,
                                First_signal_networks = new() { Green = true },
                                Constant = trailOffTicks,
                                Comparator = Comparators.GreaterThan,
                                Compare_type = CompareTypes.And
                            }
                        ],
                        Outputs =
                        [
                            new()
                            {
                                Signal = timeGatedPitchSignal,
                                Copy_count_from_input = true,
                                Networks = new() { Red = true },
                            }
                        ]
                    }
                }
            };
            entities.Add(timeGate);

            wires.Add(new((timeGate, ConnectionType.Green1), (signalBuffer, ConnectionType.Green2)));
            wires.Add(new((timeGate, ConnectionType.Red1), (pitchRenamer, ConnectionType.Red2)));

            var signalPropagator1 = new Entity
            {
                Player_description = $"Signal propagator 1 for voice {voiceIndex + 1}",
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
                            new()
                            {
                                First_signal = durationSignal,
                                Constant = 0,
                                Comparator = Comparators.GreaterThan
                            }
                        ],
                        Outputs =
                        [
                            new()
                            {
                                Signal = pitchSignal,
                                Copy_count_from_input = true
                            },
                            new()
                            {
                                Signal = instrumentSignal,
                                Copy_count_from_input = true
                            },
                            new()
                            {
                                Signal = durationSignal,
                                Copy_count_from_input = true
                            }
                        ]
                    }
                }
            };
            entities.Add(signalPropagator1);

            wires.Add(new((signalPropagator1, ConnectionType.Green1), (timeGate, ConnectionType.Green1)));
            wires.Add(new((signalPropagator1, ConnectionType.Red2), (timeGate, ConnectionType.Red2)));

            List<Entity> volumeAdjustmentPickers = [];

            foreach (var (volumeAdjustment, volumeLevelIndex) in volumeAdjustments.Select((volumeAdjustment, index) => (volumeAdjustment, index)))
            {
                var volumeAdjustmentPicker = new Entity
                {
                    Player_description = $"Volume adjustment picker {volumeLevelIndex + 1} for voice {voiceIndex + 1}",
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
                            Conditions = volumeAdjustment.Conditions,
                            Outputs =
                            [
                                new()
                                {
                                    Signal = volumeAdjustmentSignal,
                                    Copy_count_from_input = true
                                }
                            ]
                        }
                    }
                };
                entities.Add(volumeAdjustmentPicker);

                wires.Add(new((volumeAdjustmentPicker, ConnectionType.Green1), volumeLevelIndex == 0 ? (pitchRenamer, ConnectionType.Green1) : (volumeAdjustmentPickers[^1], ConnectionType.Green1)));
                wires.Add(new((volumeAdjustmentPicker, ConnectionType.Red1), voiceIndex == 0 ? (volumeAdjustmentProviders[volumeLevelIndex], ConnectionType.Red1) : (previousVolumeAdjustmentPickers[volumeLevelIndex], ConnectionType.Red1)));

                if (volumeLevelIndex > 0)
                {
                    wires.Add(new((volumeAdjustmentPicker, ConnectionType.Red2), (volumeAdjustmentPickers[^1], ConnectionType.Red2)));
                }

                volumeAdjustmentPickers.Add(volumeAdjustmentPicker);
            }

            previousVolumeAdjustmentPickers = volumeAdjustmentPickers;

            var signalPropagator2 = new Entity
            {
                Player_description = $"Signal propagator 2 for voice {voiceIndex + 1}",
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
                            new()
                            {
                                First_signal = durationSignal,
                                Constant = 0,
                                Comparator = Comparators.GreaterThan
                            }
                        ],
                        Outputs =
                        [
                            new()
                            {
                                Signal = pitchSignal,
                                Copy_count_from_input = true
                            },
                            new()
                            {
                                Signal = timeGatedPitchSignal,
                                Copy_count_from_input = true
                            },
                            new()
                            {
                                Signal = instrumentSignal,
                                Copy_count_from_input = true
                            },
                            new()
                            {
                                Signal = durationSignal,
                                Copy_count_from_input = true
                            }
                        ]
                    }
                }
            };
            entities.Add(signalPropagator2);

            wires.Add(new((signalPropagator2, ConnectionType.Red1), (signalPropagator1, ConnectionType.Red2)));

            var masterVolumeMultiplier = new Entity
            {
                Player_description = $"Master volume multiplier for voice {voiceIndex + 1}",
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
                        First_signal = volumeSignal,
                        First_signal_networks = new() { Green = true },
                        Second_signal = masterVolumeSignal,
                        Second_signal_networks = new() { Red = true },
                        Operation = ArithmeticOperations.Multiplication,
                        Output_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot)
                    }
                }
            };
            entities.Add(masterVolumeMultiplier);

            wires.Add(new((masterVolumeMultiplier, ConnectionType.Green1), (volumeAdjustmentPickers[^1], ConnectionType.Green1)));
            wires.Add(new((masterVolumeMultiplier, ConnectionType.Red1), voiceIndex == 0 ? (masterVolumeProvider, ConnectionType.Red1) : (previousMasterVolumeMultiplier, ConnectionType.Red1)));

            previousMasterVolumeMultiplier = masterVolumeMultiplier;

            var volumeAdjustmentMultiplier = new Entity
            {
                Player_description = $"Volume adjustment multiplier for voice {voiceIndex + 1}",
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
                        First_signal_networks = new() { Green = true },
                        Second_signal = volumeAdjustmentSignal,
                        Second_signal_networks = new() { Red = true },
                        Operation = ArithmeticOperations.Multiplication,
                        Output_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot)
                    }
                }
            };
            entities.Add(volumeAdjustmentMultiplier);

            wires.Add(new((volumeAdjustmentMultiplier, ConnectionType.Green1), (masterVolumeMultiplier, ConnectionType.Green2)));
            wires.Add(new((volumeAdjustmentMultiplier, ConnectionType.Red1), (volumeAdjustmentPickers[^1], ConnectionType.Red2)));

            var volumeLevelDivider = new Entity
            {
                Player_description = $"Volume level divider for voice {voiceIndex + 1}",
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
                        Second_constant = 10000,
                        Operation = ArithmeticOperations.Division,
                        Output_signal = volumeSignal
                    }
                }
            };
            entities.Add(volumeLevelDivider);

            wires.Add(new((volumeLevelDivider, ConnectionType.Green1), (volumeAdjustmentMultiplier, ConnectionType.Green2)));
            wires.Add(new((volumeLevelDivider, ConnectionType.Green2), (signalPropagator2, ConnectionType.Green2)));

            Debug.Assert(y == yOffset + headerHeight);

            for (int instrumentIndex = 0; instrumentIndex < instrumentCount; instrumentIndex++)
            {
                var instrument = (Instrument)(instrumentIndex + 3);

                var speakerController = new Entity
                {
                    Player_description = $"Speaker controller for {instrument} on voice {voiceIndex + 1}",
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
                                new()
                                {
                                    First_signal = instrumentSignal,
                                    Constant = instrumentIndex + 1,
                                    Comparator = Comparators.IsEqual,
                                },
                                new()
                                {
                                    First_signal = durationSignal,
                                    Constant = 0,
                                    Comparator = Comparators.GreaterThan,
                                    Compare_type = CompareTypes.And
                                }
                            ],
                            Outputs =
                            [
                                new()
                                {
                                    Signal = timeGatedPitchSignal,
                                    Copy_count_from_input = true
                                },
                                new()
                                {
                                    Signal = volumeSignal,
                                    Copy_count_from_input = true
                                }
                            ]
                        }
                    }
                };
                entities.Add(speakerController);
                players.Add(speakerController);

                wires.Add(new((speakerController, ConnectionType.Green1), instrumentIndex == 0 ? (volumeLevelDivider, ConnectionType.Green2) : (players[instrumentIndex - 1], ConnectionType.Green1)));

                var speaker = new Entity
                {
                    Name = ItemNames.ProgrammableSpeaker,
                    Position = new Position
                    {
                        X = columnX,
                        Y = y++
                    },
                    Control_behavior = new ControlBehavior
                    {
                        Circuit_condition = new CircuitCondition
                        {
                            First_signal = timeGatedPitchSignal
                        },
                        Circuit_parameters = new CircuitParameters
                        {
                            Signal_value_is_pitch = true,
                            Instrument_id = (int)instrument - 1
                        }
                    },
                    Parameters = new SpeakerParameters
                    {
                        Playback_mode = PlaybackModes.Global,
                        Allow_polyphony = true,
                        Volume_controlled_by_signal = true,
                        Volume_signal_id = volumeSignal
                    },
                    Alert_parameters = new SpeakerAlertParameters
                    {
                        Show_alert = false
                    }
                };
                entities.Add(speaker);

                wires.Add(new((speaker, ConnectionType.Red1), (speakerController, ConnectionType.Red2)));
            }

            var outputVolumeBuffer = new Entity
            {
                Player_description = $"Output volume buffer for voice {voiceIndex + 1}",
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
                        First_signal = volumeSignal,
                        Second_constant = 1,
                        Operation = ArithmeticOperations.Multiplication,
                        Output_signal = volumeSignal
                    }
                }
            };
            entities.Add(outputVolumeBuffer);

            wires.Add(new((outputVolumeBuffer, ConnectionType.Green1), (players[^1], ConnectionType.Green1)));

            var pitchGrouper = new Entity
            {
                Player_description = $"Output pitch grouper for voice {voiceIndex + 1}",
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
                        First_signal = pitchSignal,
                        Second_constant = pitchesPerGroup,
                        Operation = ArithmeticOperations.Division,
                        Output_signal = pitchGroupSignal
                    }
                }
            };
            entities.Add(pitchGrouper);

            wires.Add(new((pitchGrouper, ConnectionType.Green1), (outputVolumeBuffer, ConnectionType.Green1)));
            wires.Add(new((pitchGrouper, ConnectionType.Green2), (outputVolumeBuffer, ConnectionType.Green2)));
            wires.Add(new((pitchGrouper, ConnectionType.Red1), voiceIndex == 0 ? (pitchOffsetProvider, ConnectionType.Red1) : (previousPitchGrouper, ConnectionType.Red1)));

            previousPitchGrouper = pitchGrouper;

            List<Entity> instrumentOffsetPickers = [];

            foreach (var ((instruments, offset), instrumentOffsetIndex) in instrumentOffsets.Select((instrumentOffset, index) => (instrumentOffset, index)))
            {
                var instrumentOffsetPicker = new Entity
                {
                    Player_description = $"Instrument offset picker {instrumentOffsetIndex + 1} for voice {voiceIndex + 1}",
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
                            Conditions = [.. instruments.Select((instrument, instrumentIndex) => new DeciderCondition
                            {
                                First_signal = instrumentSignal,
                                Constant = (int)instrument - 2,
                                Comparator = Comparators.IsEqual,
                                Compare_type = CompareTypes.Or
                            })],
                            Outputs =
                            [
                                new()
                                {
                                    Signal = pitchGroupSignal,
                                    Copy_count_from_input = true
                                }
                            ]
                        }
                    }
                };
                entities.Add(instrumentOffsetPicker);

                wires.Add(new((instrumentOffsetPicker, ConnectionType.Green1), instrumentOffsetIndex == 0 ? (pitchGrouper, ConnectionType.Green1) : (instrumentOffsetPickers[^1], ConnectionType.Green1)));
                wires.Add(new((instrumentOffsetPicker, ConnectionType.Green2), instrumentOffsetIndex == 0 ? (pitchGrouper, ConnectionType.Green2) : (instrumentOffsetPickers[^1], ConnectionType.Green2)));
                wires.Add(new((instrumentOffsetPicker, ConnectionType.Red1), voiceIndex == 0 ? (instrumentOffsetProviders[instrumentOffsetIndex], ConnectionType.Red1) : (previousInstrumentOffsetPickers[instrumentOffsetIndex], ConnectionType.Red1)));

                instrumentOffsetPickers.Add(instrumentOffsetPicker);
            }

            previousInstrumentOffsetPickers = instrumentOffsetPickers;

            var groupVolumeBuffer = new Entity
            {
                Player_description = $"Group volume buffer for voice {voiceIndex + 1}",
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
                        First_signal = volumeSignal,
                        Second_constant = 1,
                        Operation = ArithmeticOperations.Multiplication,
                        Output_signal = volumeSignal
                    }
                }
            };
            entities.Add(groupVolumeBuffer);

            wires.Add(new((groupVolumeBuffer, ConnectionType.Green1), (instrumentOffsetPickers[^1], ConnectionType.Green2)));

            var pitchGroupMapper = new Entity
            {
                Player_description = $"Pitch group mapper for voice {voiceIndex + 1}",
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
                            new()
                            {
                                First_signal = SignalID.CreateVirtual(VirtualSignalNames.Each),
                                First_signal_networks = new() { Red = true },
                                Second_signal = pitchGroupSignal,
                                Second_signal_networks = new() { Green = true },
                                Comparator = Comparators.IsEqual
                            }
                        ],
                        Outputs =
                        [
                            new()
                            {
                                Signal = SignalID.CreateVirtual(VirtualSignalNames.Each),
                                Copy_count_from_input = false
                            }
                        ]
                    }
                }
            };
            entities.Add(pitchGroupMapper);

            wires.Add(new((pitchGroupMapper, ConnectionType.Green1), (groupVolumeBuffer, ConnectionType.Green1)));
            wires.Add(new((pitchGroupMapper, ConnectionType.Red1), voiceIndex == 0 ? (pitchGroupMappingProvider, ConnectionType.Red1) : (previousPitchGroupMapper, ConnectionType.Red1)));

            previousPitchGroupMapper = pitchGroupMapper;

            var groupVolumeMultiplier = new Entity
            {
                Player_description = $"Group volume multiplier for voice {voiceIndex + 1}",
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
                        First_signal = SignalID.CreateVirtual(VirtualSignalNames.Each),
                        First_signal_networks = new() { Red = true },
                        Second_signal = volumeSignal,
                        Second_signal_networks = new() { Green = true },
                        Operation = ArithmeticOperations.Multiplication,
                        Output_signal = SignalID.CreateVirtual(VirtualSignalNames.Each)
                    }
                }
            };
            entities.Add(groupVolumeMultiplier);

            wires.Add(new((groupVolumeMultiplier, ConnectionType.Green1), (groupVolumeBuffer, ConnectionType.Green2)));
            wires.Add(new((groupVolumeMultiplier, ConnectionType.Red1), (pitchGroupMapper, ConnectionType.Red2)));

            if (voiceIndex > 0)
            {
                wires.Add(new((groupVolumeMultiplier, ConnectionType.Green2), (previousGroupVolumeMultiplier, ConnectionType.Green2)));
            }

            previousGroupVolumeMultiplier = groupVolumeMultiplier;

            Debug.Assert(y == yOffset + headerHeight + instrumentCount * speakerCellHeight + footerHeight);

            Entity previousPitchMapper = null;
            List<Entity> displayVolumeMultipliers = [];

            for (int instrumentIndex = 0; instrumentIndex < instrumentCount; instrumentIndex++)
            {
                var instrument = (Instrument)(instrumentIndex + 3);

                var pitchMapper = new Entity
                {
                    Player_description = $"Pitch mapper for instrument {instrument} on voice {voiceIndex + 1}",
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
                                new()
                                {
                                    First_signal = SignalID.CreateVirtual(VirtualSignalNames.Each),
                                    First_signal_networks = new() { Red = true },
                                    Second_signal = pitchSignal,
                                    Second_signal_networks = new() { Green = true },
                                    Comparator = Comparators.IsEqual
                                },
                                new()
                                {
                                    First_signal = instrumentSignal,
                                    First_signal_networks = new() { Green = true },
                                    Constant = instrumentIndex + 1,
                                    Comparator = Comparators.IsEqual,
                                    Compare_type = CompareTypes.And
                                }
                            ],
                            Outputs =
                            [
                                new()
                                {
                                    Signal = SignalID.CreateVirtual(VirtualSignalNames.Each),
                                    Copy_count_from_input = false
                                }
                            ]
                        }
                    }
                };
                entities.Add(pitchMapper);

                wires.Add(new((pitchMapper, ConnectionType.Green1), instrumentIndex == 0 ? (instrumentOffsetPickers[^1], ConnectionType.Green1) : (previousPitchMapper, ConnectionType.Green1)));
                wires.Add(new((pitchMapper, ConnectionType.Red1), instrumentIndex > 0 ? (previousPitchMapper, ConnectionType.Red1) : voiceIndex > 0 ? (previousFirstPitchMapper, ConnectionType.Red1) : (pitchMappingProvider, ConnectionType.Red1)));

                if (instrumentIndex == 0)
                {
                    previousFirstPitchMapper = pitchMapper;
                }

                previousPitchMapper = pitchMapper;

                var displayVolumeMultiplier = new Entity
                {
                    Player_description = $"Display volume multiplier for instrument {instrument} on voice {voiceIndex + 1}",
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
                            First_signal = SignalID.CreateVirtual(VirtualSignalNames.Each),
                            First_signal_networks = new() { Red = true },
                            Second_signal = volumeSignal,
                            Second_signal_networks = new() { Green = true },
                            Operation = ArithmeticOperations.Multiplication,
                            Output_signal = SignalID.CreateVirtual(VirtualSignalNames.Each)
                        }
                    }
                };
                entities.Add(displayVolumeMultiplier);

                wires.Add(new((displayVolumeMultiplier, ConnectionType.Red1), (pitchMapper, ConnectionType.Red2)));
                wires.Add(new((displayVolumeMultiplier, ConnectionType.Green1), instrumentIndex == 0 ? (pitchGroupMapper, ConnectionType.Green1) : (displayVolumeMultipliers[^1], ConnectionType.Green1)));

                if (voiceIndex > 0)
                {
                    var outputConnectionType = instrumentIndex % 2 == 0 ? ConnectionType.Green2 : ConnectionType.Red2;
                    wires.Add(new((displayVolumeMultiplier, outputConnectionType), (previousDisplayVolumeMultipliers[instrumentIndex], outputConnectionType)));
                }

                displayVolumeMultipliers.Add(displayVolumeMultiplier);
            }

            previousDisplayVolumeMultipliers = displayVolumeMultipliers;

            Debug.Assert(y == yOffset + headerHeight + instrumentCount * (speakerCellHeight + displayCellHeight) + footerHeight);
        }

        if (includePower)
        {
            var substationWidth = (channelCount + 7) / 16 + 1;
            var substationHeight = (gridHeight + 3) / 18 + 1;

            AddSubstations(entities, wires, substationWidth, substationHeight, xOffset, yOffset + 2);
        }

        BlueprintUtil.PopulateEntityNumbers(entities);

        if (isHorizontal)
        {
            foreach (var entity in entities)
            {
                (entity.Position.Y, entity.Position.X) = (entity.Position.X, entity.Position.Y);

                entity.Direction = entity.Direction switch
                {
                    Direction.Up => Direction.Left,
                    Direction.Left => Direction.Up,
                    Direction.Down => Direction.Right,
                    Direction.Right => Direction.Down,
                    null => null,
                    _ => throw new InvalidOperationException($"Unexpected direction {entity.Direction}")
                };
            }
        }

        return new Blueprint
        {
            Label = $"{channelCount}x{instrumentCount} Music Box Speaker",
            Icons = [Icon.Create(ItemNames.ProgrammableSpeaker)],
            Entities = entities,
            Wires = wires.ToArrayList()
        };
    }
}

public class MusicBoxV2SpeakerConfiguration
{
    public int? InstrumentCount { get; set; }
    public int? ChannelCount { get; set; }
    public bool? IncludePower { get; set; }
    public bool? IsHorizontal { get; set; }
}
