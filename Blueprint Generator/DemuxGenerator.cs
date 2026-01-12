using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Constants;
using BlueprintGenerator.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace BlueprintGenerator
{
    public class DemuxGenerator : IBlueprintGenerator
    {
        public Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<DemuxConfiguration>());
        }

        public static Blueprint Generate(DemuxConfiguration configuration)
        {
            var signalCount = configuration.SignalCount ?? ComputerSignals.OrderedSignals.Count;
            var width = configuration.Width ?? 1;
            var addressSignal = configuration.AddressSignal ?? VirtualSignalNames.Dot;
            var outputSignal = configuration.OutputSignal ?? VirtualSignalNames.LetterOrDigit('A');

            var entities = new List<Entity>();
            var wires = new List<Wire>();
            var signalFilters = new SignalFilter[signalCount];

            for (int index = 0; index < signalCount; index++)
            {
                var filterX = index % width * 4;
                var filterY = index / width;

                var addressChecker = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = 0.5 + filterX,
                        Y = filterY
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
                                    First_signal = SignalID.Create(addressSignal),
                                    Constant = index + 1,
                                    Comparator = Comparators.IsEqual
                                }
                            ],
                            Outputs =
                            [
                                new()
                                {
                                    Signal = SignalID.Create(VirtualSignalNames.Dot),
                                    Copy_count_from_input = false
                                }
                            ]
                        }
                    }
                };
                entities.Add(addressChecker);

                var signalRenamer = new Entity
                {
                    Name = ItemNames.ArithmeticCombinator,
                    Position = new Position
                    {
                        X = 2.5 + filterX,
                        Y = filterY
                    },
                    Direction = Direction.Right,
                    Control_behavior = new ControlBehavior
                    {
                        Arithmetic_conditions = new ArithmeticConditions
                        {
                            First_signal = SignalID.Create(ComputerSignals.OrderedSignals[index]),
                            Second_signal = SignalID.Create(VirtualSignalNames.Dot),
                            Operation = ArithmeticOperations.Multiplication,
                            Output_signal = SignalID.Create(outputSignal)
                        }
                    }
                };
                entities.Add(signalRenamer);

                signalFilters[index] = new SignalFilter
                {
                    AddressChecker = addressChecker,
                    SignalRenamer = signalRenamer
                };
            }

            BlueprintUtil.PopulateEntityNumbers(entities);

            for (var index = 0; index < signalCount; index++)
            {
                var signalFilter = signalFilters[index];

                wires.Add(new((signalFilter.AddressChecker, ConnectionType.Red2), (signalFilter.SignalRenamer, ConnectionType.Red1)));

                if (index > 0)
                {
                    var adjacentSignalFilterIndex = index / width == 0 ? index - 1 : index - width;
                    var adjacentSignalFilter = signalFilters[adjacentSignalFilterIndex];

                    wires.Add(new((signalFilter.AddressChecker, ConnectionType.Red1), (adjacentSignalFilter.AddressChecker, ConnectionType.Red1)));
                    wires.Add(new((signalFilter.SignalRenamer, ConnectionType.Green1), (adjacentSignalFilter.SignalRenamer, ConnectionType.Green1)));
                    wires.Add(new((signalFilter.SignalRenamer, ConnectionType.Red2), (adjacentSignalFilter.SignalRenamer, ConnectionType.Red2)));

                }
            }

            return new Blueprint
            {
                Label = "Demultiplexer",
                Icons =
                [
                    Icon.Create(ItemNames.DeciderCombinator),
                    Icon.Create(ItemNames.ArithmeticCombinator)
                ],
                Entities = entities,
                Wires = wires.ToArrayList()
            };
        }

        private class SignalFilter
        {
            public Entity AddressChecker { get; set; }
            public Entity SignalRenamer { get; set; }
        }
    }

    public class DemuxConfiguration
    {
        public int? SignalCount { get; set; }
        public int? Width { get; set; }
        public string AddressSignal { get; set; }
        public string OutputSignal { get; set; }
    }
}
