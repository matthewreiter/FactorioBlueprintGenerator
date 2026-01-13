using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Constants;
using BlueprintGenerator.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Diagnostics;

namespace BlueprintGenerator;

public class MusicBoxV2DemuxGenerator : IBlueprintGenerator
{
    public Blueprint Generate(IConfigurationRoot configuration)
    {
        return Generate();
    }

    public static Blueprint Generate()
    {
        var signalCount = SpeakerChannelSignals.Signals.Count;
        var signalGroupCount = SpeakerChannelSignals.AdditionalSignalGroups.Count;
        var elapsedTimeSignal = SignalID.CreateVirtual(VirtualSignalNames.Clock);

        var entities = new List<Entity>();
        var wires = new List<Wire>();

        Entity previousTimeChecker = null;

        for (int signalGroupIndex = 0; signalGroupIndex < signalGroupCount; signalGroupIndex++)
        {
            var signalGroup = SpeakerChannelSignals.AdditionalSignalGroups[signalGroupIndex];
            Debug.Assert(signalGroup.Count == signalCount);

            var timeSignal = SignalID.CreateLetterOrDigit((char)('A' + signalGroupIndex));

            var timeChecker = new Entity
            {
                Name = ItemNames.DeciderCombinator,
                Position = new Position
                {
                    X = 0.5 + signalGroupIndex * 2,
                    Y = 0
                },
                Direction = Direction.Right,
                Control_behavior = new ControlBehavior
                {
                    Decider_conditions = new DeciderConditions
                    {
                        Conditions =
                        [
                            new()
                            {
                                First_signal = timeSignal,
                                Constant = 0,
                                Comparator = Comparators.IsNotEqual,
                                First_signal_networks = new() { Green = true }
                            },
                            new()
                            {
                                First_signal = elapsedTimeSignal,
                                Second_signal = timeSignal,
                                Comparator = Comparators.IsEqual,
                                First_signal_networks = new() { Green = true },
                                Second_signal_networks = new() { Green = true },
                                Compare_type = CompareTypes.And
                            }
                        ],
                        Outputs =
                        [
                            new()
                            {
                                Signal = SignalID.CreateVirtual(VirtualSignalNames.Everything),
                                Copy_count_from_input = true,
                                Networks = new() { Red = true }
                            }
                        ],
                    }
                }
            };
            entities.Add(timeChecker);

            if (signalGroupIndex > 0)
            {
                wires.Add(new((timeChecker, ConnectionType.Green1), (previousTimeChecker, ConnectionType.Green1)));
                wires.Add(new((timeChecker, ConnectionType.Green2), (previousTimeChecker, ConnectionType.Green2)));
            }

            previousTimeChecker = timeChecker;

            Entity previousSignalRenamer = null;

            for (int signalIndex = 0; signalIndex < signalCount; signalIndex++)
            {
                var inputSignal = SignalID.Create(signalGroup[signalIndex]);
                var outputSignal = SignalID.Create(SpeakerChannelSignals.Signals[signalIndex]);

                var signalRenamer = new Entity
                {
                    Name = ItemNames.ArithmeticCombinator,
                    Position = new Position
                    {
                        X = 0.5 + signalGroupIndex * 2,
                        Y = 1 + signalIndex
                    },
                    Direction = Direction.Right,
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

                wires.Add(new((signalRenamer, ConnectionType.Green1), signalIndex == 0 ? (timeChecker, ConnectionType.Green1) : (previousSignalRenamer, ConnectionType.Green1)));
                wires.Add(new((signalRenamer, ConnectionType.Red2), signalIndex == 0 ? (timeChecker, ConnectionType.Red1) : (previousSignalRenamer, ConnectionType.Red2)));

                previousSignalRenamer = signalRenamer;
            }
        }

        BlueprintUtil.PopulateEntityNumbers(entities);

        return new Blueprint
        {
            Label = "Music Box Demultiplexer",
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
