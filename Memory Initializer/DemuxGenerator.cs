using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using MemoryInitializer.Constants;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using static MemoryInitializer.ConnectionUtil;

namespace MemoryInitializer
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
                            First_signal = SignalID.Create(addressSignal),
                            Constant = index + 1,
                            Comparator = Comparators.IsEqual,
                            Output_signal = SignalID.Create(VirtualSignalNames.Dot),
                            Copy_count_from_input = false
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

                AddConnection(CircuitColor.Red, signalFilter.AddressChecker, CircuitId.Output, signalFilter.SignalRenamer, CircuitId.Input);

                if (index > 0)
                {
                    var adjacentSignalFilterIndex = index / width == 0 ? index - 1 : index - width;
                    var adjacentSignalFilter = signalFilters[adjacentSignalFilterIndex];

                    AddConnection(CircuitColor.Red, signalFilter.AddressChecker, CircuitId.Input, adjacentSignalFilter.AddressChecker, CircuitId.Input);
                    AddConnection(CircuitColor.Green, signalFilter.SignalRenamer, CircuitId.Input, adjacentSignalFilter.SignalRenamer, CircuitId.Input);
                    AddConnection(CircuitColor.Red, signalFilter.SignalRenamer, CircuitId.Output, adjacentSignalFilter.SignalRenamer, CircuitId.Output);
                }
            }

            return new Blueprint
            {
                Label = $"Demultiplexer",
                Icons = new List<Icon>
                {
                    Icon.Create(ItemNames.DeciderCombinator),
                    Icon.Create(ItemNames.ArithmeticCombinator)
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
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
