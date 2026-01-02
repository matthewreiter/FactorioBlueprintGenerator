using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static BlueprintGenerator.PowerUtil;

namespace BlueprintGenerator;

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

        const int maxInstruments = 10;
        const int maxPitches = 48;
        const int maxVolumes = 100;
        const int ticksPerCycle = 4;
        const int trailOffTicks = 10;

        var width = channelCount;
        var height = instrumentCount;

        const int headerHeight = 24;
        const int cellHeight = 3;

        var gridWidth = width + (includePower ? ((width + 7) / 16 + 1) * 2 : 0);
        var gridHeight = headerHeight + height * cellHeight;
        var xOffset = -gridWidth / 2;
        var yOffset = -gridHeight / 2;

        var durationSignal = SignalID.CreateVirtual(VirtualSignalNames.Wait);
        var elapsedTimeSignal = SignalID.CreateVirtual(VirtualSignalNames.Clock);
        var remainingTimeSignal = SignalID.CreateVirtual(VirtualSignalNames.Sun);
        var modularTimeSignal = SignalID.CreateVirtual(VirtualSignalNames.Speed);
        var instrumentSignal = SignalID.CreateVirtual(VirtualSignalNames.Snowflake);
        var pitchSignal = SignalID.CreateVirtual(VirtualSignalNames.Explosion);
        var volumeSignal = SignalID.CreateVirtual(VirtualSignalNames.Alarm);

        var entities = new List<Entity>();
        var wires = new List<Wire>();
        Entity previousDurationDivider = null;
        Entity previousInstrumentDivider = null;

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

        for (int column = 0; column < width; column++)
        {
            var columnX = column + (includePower ? (column / 16 + 1) * 2 : 0) + xOffset;
            var y = yOffset;
            var inputSignal = SignalID.CreateLetterOrDigit((char)('A' + column));
            var players = new List<Entity>();

            var durationDivider = new Entity
            {
                Player_description = "Duration divider",
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

            wires.Add(new((durationDivider, ConnectionType.Red1), column == 0 ? (inputBuffer, ConnectionType.Red2) : (previousDurationDivider, ConnectionType.Red1)));

            previousDurationDivider = durationDivider;

            var instrumentDivider = new Entity
            {
                Player_description = "Instrument divider",
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

            wires.Add(new((instrumentDivider, ConnectionType.Green1), column == 0 ? (inputBuffer, ConnectionType.Green1) : (previousInstrumentDivider, ConnectionType.Green1)));

            previousInstrumentDivider = instrumentDivider;

            var instrumentExtractor = new Entity
            {
                Player_description = "Instrument extractor",
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
            wires.Add(new((instrumentExtractor, ConnectionType.Green2), (durationDivider, ConnectionType.Green2)));

            var pitchDivider = new Entity
            {
                Player_description = "Pitch divider",
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
                Player_description = "Pitch extractor",
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
            wires.Add(new((pitchExtractor, ConnectionType.Green2), (instrumentExtractor, ConnectionType.Green2)));

            var volumeExtractor = new Entity
            {
                Player_description = "Volume extractor",
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
            wires.Add(new((volumeExtractor, ConnectionType.Green2), (pitchExtractor, ConnectionType.Green2)));

            var elapsedTimeIncrementer = new Entity
            {
                Player_description = "Elapsed time incrementer",
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

            wires.Add(new((elapsedTimeIncrementer, ConnectionType.Green1), (volumeExtractor, ConnectionType.Green2)));

            var currentNoteMemory = new Entity
            {
                Player_description = "Current note memory",
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
                                First_signal = SignalID.CreateVirtual(VirtualSignalNames.Deny),
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
            wires.Add(new((currentNoteMemory, ConnectionType.Green1), (elapsedTimeIncrementer, ConnectionType.Green1)));

            var timeDivider = new Entity
            {
                Player_description = "Time divider",
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
                Player_description = "Remaining time calculator",
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
                Player_description = "Offsetter",
                Name = ItemNames.ConstantCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = y++
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Sections = Sections.Create([Filter.Create(pitchSignal, 1), Filter.Create(volumeSignal, 1)])
                }
            };
            entities.Add(offsetter);

            wires.Add(new((offsetter, ConnectionType.Green1), (remainingTimeCalculator, ConnectionType.Green2)));

            var timeGate = new Entity
            {
                Player_description = "Time gate",
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
                                Constant = 1,
                                Comparator = Comparators.IsEqual
                            },
                            new()
                            {
                                First_signal = modularTimeSignal,
                                Constant = 1,
                                Comparator = Comparators.IsEqual,
                                Compare_type = CompareTypes.Or
                            },
                            new()
                            {
                                First_signal = remainingTimeSignal,
                                Constant = trailOffTicks,
                                Comparator = Comparators.GreaterThan,
                                Compare_type = CompareTypes.And
                            }
                        ],
                        Outputs =
                        [
                            new()
                            {
                                Signal = pitchSignal,
                                Copy_count_from_input = true
                            }
                        ]
                    }
                }
            };
            entities.Add(timeGate);

            wires.Add(new((timeGate, ConnectionType.Green1), (offsetter, ConnectionType.Green1)));

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
                            new()
                            {
                                First_signal = instrumentSignal,
                                Constant = 0,
                                Comparator = Comparators.GreaterThan
                            }
                        ],
                        Outputs =
                        [
                            new()
                            {
                                Signal = instrumentSignal,
                                Copy_count_from_input = true
                            },
                            new()
                            {
                                Signal = volumeSignal,
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
            entities.Add(signalPropagator);

            wires.Add(new((signalPropagator, ConnectionType.Green1), (timeGate, ConnectionType.Green1)));
            wires.Add(new((signalPropagator, ConnectionType.Green2), (timeGate, ConnectionType.Green2)));

            Debug.Assert(y == yOffset + headerHeight);

            for (int row = 0; row < height; row++)
            {
                var speakerController = new Entity
                {
                    Player_description = $"Speaker controller for instrument {row}",
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
                                    Constant = row,
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
                                    Signal = pitchSignal,
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

                wires.Add(new((speakerController, ConnectionType.Green1), row == 0 ? (signalPropagator, ConnectionType.Green2) : (players[row - 1], ConnectionType.Green1)));

                var speaker = new Entity
                {
                    Player_description = $"Speaker for instrument {row}",
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
                            First_signal = pitchSignal
                        },
                        Circuit_parameters = new CircuitParameters
                        {
                            Signal_value_is_pitch = true,
                            Instrument_id = row + 2
                        }
                    },
                    Parameters = new SpeakerParameter
                    {
                        Playback_mode = PlaybackModes.Global,
                        Allow_polyphony = true,
                        Volume_controlled_by_signal = true,
                        Volume_signal_id = volumeSignal
                    },
                    Alert_parameters = new SpeakerAlertParameter
                    {
                        Show_alert = false
                    }
                };
                entities.Add(speaker);

                wires.Add(new((speaker, ConnectionType.Red1), (speakerController, ConnectionType.Red2)));
            }
        }

        if (includePower)
        {
            var substationWidth = (width + 7) / 16 + 1;
            var substationHeight = (gridHeight + 3) / 18 + 1;

            AddSubstations(entities, wires, substationWidth, substationHeight, xOffset, yOffset + 2);
        }

        BlueprintUtil.PopulateEntityNumbers(entities);

        return new Blueprint
        {
            Label = $"{width}x{height} Music Box Speaker",
            Icons =
            [
                new()
                {
                    Signal = SignalID.Create(ItemNames.ProgrammableSpeaker)
                }
            ],
            Entities = entities,
            Wires = [.. wires.Select(wire => wire.ToArray())],
            Item = ItemNames.Blueprint,
            Version = BlueprintVersions.CurrentVersion
        };
    }
}

public class MusicBoxV2SpeakerConfiguration
{
    public int? InstrumentCount { get; set; }
    public int? ChannelCount { get; set; }
    public bool? IncludePower { get; set; }
}
