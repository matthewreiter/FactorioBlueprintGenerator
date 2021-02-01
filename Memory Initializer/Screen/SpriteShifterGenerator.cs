using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using static MemoryInitializer.ConnectionUtil;

namespace MemoryInitializer.Screen
{
    public static class SpriteShifterGenerator
    {
        public static Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<SpriteShifterConfiguration>());
        }

        public static Blueprint Generate(SpriteShifterConfiguration configuration)
        {
            var signalCount = configuration.SignalCount ?? ScreenUtil.PixelSignals.Count;

            const int maxFilters = 20;

            var inputSignals = Enumerable.Range('0', 10).Concat(Enumerable.Range('A', 22))
                .Select(letterOrDigit => VirtualSignalNames.LetterOrDigit((char)letterOrDigit))
                .ToList();

            var entities = new List<Entity>();
            var outputMaps = new Entity[(signalCount + maxFilters - 1) / maxFilters];
            var inputMaps = new Entity[inputSignals.Count];
            var inputCheckers = new Entity[inputSignals.Count];
            var inputBuffers = new Entity[inputSignals.Count];
            var outputGenerators = new Entity[inputSignals.Count];
            var outputCleaners = new Entity[inputSignals.Count];

            // Output signal maps
            for (var index = 0; index < outputMaps.Length; index++)
            {
                var outputSignalMap = new Entity
                {
                    Name = ItemNames.ConstantCombinator,
                    Position = new Position
                    {
                        X = index + 1,
                        Y = 0
                    },
                    Direction = Direction.Right,
                    Control_behavior = new ControlBehavior
                    {
                        Filters = ScreenUtil.PixelSignals.Skip(index * maxFilters).Take(Math.Min(maxFilters, signalCount - index * maxFilters)).Select((signal, signalIndex) => new Filter
                        {
                            Signal = SignalID.Create(signal),
                            Count = index * maxFilters + signalIndex + 1
                        }).ToList()
                    }
                };
                outputMaps[index] = outputSignalMap;
                entities.Add(outputSignalMap);
            }

            var inputSquared = new Entity
            {
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = 1.5,
                    Y = 1
                },
                Direction = Direction.Right,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new ArithmeticConditions
                    {
                        First_signal = SignalID.Create(VirtualSignalNames.Each),
                        Second_constant = 2,
                        Operation = ArithmeticOperations.Exponentiation,
                        Output_signal = SignalID.Create(VirtualSignalNames.Each)
                    }
                }
            };
            entities.Add(inputSquared);

            var bufferedInput = new Entity
            {
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = 3.5,
                    Y = 1
                },
                Direction = Direction.Right,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new ArithmeticConditions
                    {
                        First_signal = SignalID.Create(VirtualSignalNames.Each),
                        Second_constant = 1,
                        Operation = ArithmeticOperations.Multiplication,
                        Output_signal = SignalID.Create(VirtualSignalNames.Each)
                    }
                }
            };
            entities.Add(bufferedInput);

            var negativeInputSquared = new Entity
            {
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = 5.5,
                    Y = 1
                },
                Direction = Direction.Right,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new ArithmeticConditions
                    {
                        First_signal = SignalID.Create(VirtualSignalNames.Each),
                        Second_constant = -1,
                        Operation = ArithmeticOperations.Multiplication,
                        Output_signal = SignalID.Create(VirtualSignalNames.Each)
                    }
                }
            };
            entities.Add(negativeInputSquared);

            // Input signal processors
            for (var index = 0; index < inputSignals.Count; index++)
            {
                var inputSignal = inputSignals[index];
                var y = index + 2;

                var inputMap = new Entity
                {
                    Name = ItemNames.ConstantCombinator,
                    Position = new Position
                    {
                        X = 0,
                        Y = y
                    },
                    Direction = Direction.Right,
                    Control_behavior = new ControlBehavior
                    {
                        Filters = new List<Filter> { new Filter { Signal = SignalID.Create(VirtualSignalNames.Dot), Count = index + 1 } }
                    }
                };
                inputMaps[index] = inputMap;
                entities.Add(inputMap);

                var inputChecker = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = 1.5,
                        Y = y
                    },
                    Direction = Direction.Right,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Each),
                            Second_signal = SignalID.Create(VirtualSignalNames.Dot),
                            Comparator = Comparators.IsEqual,
                            Output_signal = SignalID.Create(VirtualSignalNames.Each),
                            Copy_count_from_input = false
                        }
                    }
                };
                inputCheckers[index] = inputChecker;
                entities.Add(inputChecker);

                var inputBuffer = new Entity
                {
                    Name = ItemNames.ArithmeticCombinator,
                    Position = new Position
                    {
                        X = 3.5,
                        Y = y
                    },
                    Direction = Direction.Right,
                    Control_behavior = new ControlBehavior
                    {
                        Arithmetic_conditions = new ArithmeticConditions
                        {
                            First_signal = SignalID.Create(inputSignal),
                            Second_constant = 1,
                            Operation = ArithmeticOperations.Multiplication,
                            Output_signal = SignalID.Create(inputSignal)
                        }
                    }
                };
                inputBuffers[index] = inputBuffer;
                entities.Add(inputBuffer);

                var outputGenerator = new Entity
                {
                    Name = ItemNames.ArithmeticCombinator,
                    Position = new Position
                    {
                        X = 5.5,
                        Y = y
                    },
                    Direction = Direction.Right,
                    Control_behavior = new ControlBehavior
                    {
                        Arithmetic_conditions = new ArithmeticConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Each),
                            Second_signal = SignalID.Create(inputSignal),
                            Operation = ArithmeticOperations.Multiplication,
                            Output_signal = SignalID.Create(VirtualSignalNames.Each)
                        }
                    }
                };
                outputGenerators[index] = outputGenerator;
                entities.Add(outputGenerator);

                var outputCleaner = new Entity
                {
                    Name = ItemNames.ArithmeticCombinator,
                    Position = new Position
                    {
                        X = 7.5,
                        Y = y
                    },
                    Direction = Direction.Right,
                    Control_behavior = new ControlBehavior
                    {
                        Arithmetic_conditions = new ArithmeticConditions
                        {
                            First_signal = SignalID.Create(inputSignal),
                            Second_constant = -1,
                            Operation = ArithmeticOperations.Multiplication,
                            Output_signal = SignalID.Create(VirtualSignalNames.Dot)
                        }
                    }
                };
                outputCleaners[index] = outputCleaner;
                entities.Add(outputCleaner);
            }

            BlueprintUtil.PopulateEntityNumbers(entities);

            // Output signal map connections
            for (var index = 1; index < outputMaps.Length; index++)
            {
                var outputSignalMap = outputMaps[index];
                var adjacentOutputSignalMap = outputMaps[index - 1];

                AddConnection(CircuitColor.Red, outputSignalMap, null, adjacentOutputSignalMap, null);
                AddConnection(CircuitColor.Green, outputSignalMap, null, adjacentOutputSignalMap, null);
            }

            AddConnection(CircuitColor.Green, inputSquared, CircuitId.Input, bufferedInput, CircuitId.Input);
            AddConnection(CircuitColor.Red, inputSquared, CircuitId.Output, negativeInputSquared, CircuitId.Input);
            AddConnection(CircuitColor.Green, outputMaps[0], null, inputCheckers[0], CircuitId.Input);
            AddConnection(CircuitColor.Green, bufferedInput, CircuitId.Input, inputBuffers[0], CircuitId.Input);
            AddConnection(CircuitColor.Green, bufferedInput, CircuitId.Output, outputCleaners[0], CircuitId.Input);
            AddConnection(CircuitColor.Green, negativeInputSquared, CircuitId.Output, outputGenerators[0], CircuitId.Output);
            AddConnection(CircuitColor.Green, outputGenerators[0], CircuitId.Output, outputCleaners[0], CircuitId.Output);

            // Input signal processor connections
            for (var index = 0; index < inputSignals.Count; index++)
            {
                var inputMap = inputMaps[index];
                var inputChecker = inputCheckers[index];
                var inputBuffer = inputBuffers[index];
                var outputGenerator = outputGenerators[index];
                var outputCleaner = outputCleaners[index];

                AddConnection(CircuitColor.Red, inputMap, null, inputChecker, CircuitId.Input);
                AddConnection(CircuitColor.Red, inputChecker, CircuitId.Output, inputBuffer, CircuitId.Output);
                AddConnection(CircuitColor.Red, inputBuffer, CircuitId.Output, outputGenerator, CircuitId.Input);

                var adjacentProcessor = index - 1;
                if (adjacentProcessor >= 0)
                {
                    var adjacentInputChecker = inputCheckers[adjacentProcessor];
                    var adjacentInputBuffer = inputBuffers[adjacentProcessor];
                    var adjacentOutputGenerator = outputGenerators[adjacentProcessor];
                    var adjacentOutputCleaner = outputCleaners[adjacentProcessor];

                    AddConnection(CircuitColor.Green, inputChecker, CircuitId.Input, adjacentInputChecker, CircuitId.Input);
                    AddConnection(CircuitColor.Green, inputBuffer, CircuitId.Input, adjacentInputBuffer, CircuitId.Input);
                    AddConnection(CircuitColor.Green, outputGenerator, CircuitId.Output, adjacentOutputGenerator, CircuitId.Output);
                    AddConnection(CircuitColor.Green, outputCleaner, CircuitId.Input, adjacentOutputCleaner, CircuitId.Input);
                    AddConnection(CircuitColor.Green, outputCleaner, CircuitId.Output, adjacentOutputCleaner, CircuitId.Output);
                }
            }

            return new Blueprint
            {
                Label = $"Sprite Shifter",
                Icons = new List<Icon>
                {
                    new Icon
                    {
                        Signal = SignalID.Create(ItemNames.DeciderCombinator)
                    },
                    new Icon
                    {
                        Signal = SignalID.Create(ItemNames.DeciderCombinator)
                    }
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };
        }
    }

    public class SpriteShifterConfiguration
    {
        public int? SignalCount { get; init; }
    }
}
