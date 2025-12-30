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
        const int maxVolumes = 101;

        var width = channelCount;
        var height = instrumentCount;

        const int headerHeight = 21;
        const int cellHeight = 3;

        var gridWidth = width + (includePower ? ((width + 7) / 16 + 1) * 2 : 0);
        var gridHeight = headerHeight + height * cellHeight;
        var xOffset = -gridWidth / 2;
        var yOffset = -gridHeight / 2;

        var durationSignal = SignalID.CreateVirtual(VirtualSignalNames.Wait);
        var instrumentSignal = SignalID.CreateVirtual(VirtualSignalNames.Snowflake);
        var pitchSignal = SignalID.CreateVirtual(VirtualSignalNames.Explosion);
        var volumeSignal = SignalID.CreateVirtual(VirtualSignalNames.Alarm);

        var entities = new List<Entity>();
        var wires = new List<Wire>();
        Entity previousDurationDivider = null;
        Entity previousInstrumentDivider = null;

        for (int column = 0; column < width; column++)
        {
            var columnX = column + (includePower ? (column / 16 + 1) * 2 : 0) + xOffset;
            var y = yOffset;
            var inputSignal = SignalID.CreateLetterOrDigit((char)('A' + column));
            var players = new List<Entity>();

            var durationDivider = new Entity
            {
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

            if (previousDurationDivider is not null)
            {
                wires.Add(new((durationDivider, ConnectionType.Red1), (previousDurationDivider, ConnectionType.Red1)));
            }

            previousDurationDivider = durationDivider;

            var instrumentDivider = new Entity
            {
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

            if (previousInstrumentDivider is not null)
            {
                wires.Add(new((instrumentDivider, ConnectionType.Green1), (previousInstrumentDivider, ConnectionType.Green1)));
            }

            previousInstrumentDivider = instrumentDivider;

            var instrumentExtractor = new Entity
            {
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

            var decrementer = new Entity
            {
                Name = ItemNames.ConstantCombinator,
                Position = new Position
                {
                    X = columnX,
                    Y = y++
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Sections = Sections.Create([Filter.Create(durationSignal, -1)])
                },
            };
            entities.Add(decrementer);

            wires.Add(new((decrementer, ConnectionType.Green1), (volumeExtractor, ConnectionType.Green2)));

            var remainingDurationMemory = new Entity
            {
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
            entities.Add(remainingDurationMemory);

            wires.Add(new((remainingDurationMemory, ConnectionType.Red1), (remainingDurationMemory, ConnectionType.Red2)));
            wires.Add(new((remainingDurationMemory, ConnectionType.Green1), (decrementer, ConnectionType.Green1)));

            var timeDivider = new Entity
            {
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
                        Second_constant = 3,
                        Operation = ArithmeticOperations.Modulus,
                        Output_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot)
                    }
                }
            };
            entities.Add(timeDivider);

            wires.Add(new((timeDivider, ConnectionType.Green1), (decrementer, ConnectionType.Green2)));

            var timeGate = new Entity
            {
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
                                First_signal = SignalID.CreateVirtual(VirtualSignalNames.Dot),
                                Constant = 0,
                                Comparator = Comparators.IsEqual
                            }
                        ],
                        Outputs =
                        [
                            new()
                            {
                                Signal = pitchSignal,
                                Copy_count_from_input = true,
                                Networks = new() { Red = true }
                            }
                        ]
                    }
                }
            };
            entities.Add(timeGate);

            wires.Add(new((timeGate, ConnectionType.Green1), (timeDivider, ConnectionType.Green2)));
            wires.Add(new((timeGate, ConnectionType.Red1), (remainingDurationMemory, ConnectionType.Red2)));

            var signalPropagator = new Entity
            {
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
                            }
                        ]
                    }
                }
            };
            entities.Add(signalPropagator);

            wires.Add(new((signalPropagator, ConnectionType.Red1), (timeGate, ConnectionType.Red1)));
            wires.Add(new((signalPropagator, ConnectionType.Green2), (timeGate, ConnectionType.Green2)));

            Debug.Assert(y == yOffset + headerHeight);

            for (int row = 0; row < height; row++)
            {
                var player = new Entity
                {
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
                                    Constant = row + 1,
                                    Comparator = Comparators.IsEqual,
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
                entities.Add(player);
                players.Add(player);

                wires.Add(new((player, ConnectionType.Green1), row == 0 ? (signalPropagator, ConnectionType.Green2) : (players[row - 1], ConnectionType.Green1)));

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

                wires.Add(new((speaker, ConnectionType.Red1), (player, ConnectionType.Red2)));
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
