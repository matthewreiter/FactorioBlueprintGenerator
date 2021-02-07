using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using static MemoryInitializer.ConnectionUtil;
using static MemoryInitializer.PowerUtil;

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
            const int shifterCount = 32;

            var inputSignals = Enumerable.Range('0', 10).Concat(Enumerable.Range('A', 22))
                .Select(letterOrDigit => VirtualSignalNames.LetterOrDigit((char)letterOrDigit))
                .ToList();
            var inputSignalCount = inputSignals.Count;

            var entities = new List<Entity>();
            var inputMaps = new Entity[inputSignalCount];
            var shifters = new Shifter[shifterCount];

            // Input maps
            for (var processorIndex = 0; processorIndex < inputSignalCount; processorIndex++)
            {
                var inputSignal = inputSignals[processorIndex];
                var y = processorIndex * 4 + 3;

                if (y % 18 == 8)
                {
                    y--;
                }
                else if (y % 18 == 9)
                {
                    y++;
                }

                var inputMap = new Entity
                {
                    Name = ItemNames.ConstantCombinator,
                    Position = new Position
                    {
                        X = 1,
                        Y = y
                    },
                    Direction = Direction.Right,
                    Control_behavior = new ControlBehavior
                    {
                        Filters = new List<Filter> { new Filter { Signal = SignalID.Create(VirtualSignalNames.Dot), Count = processorIndex + 1 } }
                    }
                };
                inputMaps[processorIndex] = inputMap;
                entities.Add(inputMap);
            }

            // Shifters
            for (var shifterIndex = 0; shifterIndex < shifterCount; shifterIndex++)
            {
                var shifterX = shifterIndex * 2 + shifterIndex / 8 * 2 + 2;

                var inputSquared = new Entity
                {
                    Name = ItemNames.ArithmeticCombinator,
                    Position = new Position
                    {
                        X = shifterX,
                        Y = 0.5
                    },
                    Direction = Direction.Down,
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
                        X = 0.5 + shifterX,
                        Y = 2
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
                        X = 1 + shifterX,
                        Y = 0.5
                    },
                    Direction = Direction.Up,
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
                var signalProcessors = new SignalProcessor[inputSignalCount];
                for (var processorIndex = 0; processorIndex < inputSignalCount; processorIndex++)
                {
                    var inputSignal = inputSignals[processorIndex];
                    var y = processorIndex * 4 + 3;

                    var inputChecker = new Entity
                    {
                        Name = ItemNames.DeciderCombinator,
                        Position = new Position
                        {
                            X = 0.5 + shifterX,
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
                    entities.Add(inputChecker);

                    var inputBuffer = new Entity
                    {
                        Name = ItemNames.ArithmeticCombinator,
                        Position = new Position
                        {
                            X = 0.5 + shifterX,
                            Y = 1 + y
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
                    entities.Add(inputBuffer);

                    var outputGenerator = new Entity
                    {
                        Name = ItemNames.ArithmeticCombinator,
                        Position = new Position
                        {
                            X = 0.5 + shifterX,
                            Y = 2 + y
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
                    entities.Add(outputGenerator);

                    var outputCleaner = new Entity
                    {
                        Name = ItemNames.ArithmeticCombinator,
                        Position = new Position
                        {
                            X = 0.5 + shifterX,
                            Y = 3 + y
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
                    entities.Add(outputCleaner);

                    signalProcessors[processorIndex] = new SignalProcessor
                    {
                        InputChecker = inputChecker,
                        InputBuffer = inputBuffer,
                        OutputGenerator = outputGenerator,
                        OutputCleaner = outputCleaner
                    };
                }

                // Output signal maps
                var outputMaps = new Entity[(signalCount + maxFilters - 1) / maxFilters];
                for (var index = 0; index < outputMaps.Length; index++)
                {
                    var outputSignalMap = new Entity
                    {
                        Name = ItemNames.ConstantCombinator,
                        Position = new Position
                        {
                            X = index % 2 + shifterX,
                            Y = index / 2 + inputSignalCount * 4 + 3
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

                shifters[shifterIndex] = new Shifter
                {
                    InputSquared = inputSquared,
                    BufferedInput = bufferedInput,
                    NegativeInputSquared = negativeInputSquared,
                    SignalProcessors = signalProcessors,
                    OutputMaps = outputMaps
                };
            }

            BlueprintUtil.PopulateEntityNumbers(entities);

            var substationWidth = shifterCount / 8 + 1;
            var substationHeight = (inputSignalCount * 4 + 3) / 18 + 1;
            entities.AddRange(CreateSubstations(substationWidth, substationHeight, 0, 8, entities.Count + 1));

            for (var shifterIndex = 0; shifterIndex < shifters.Length; shifterIndex++)
            {
                var shifter = shifters[shifterIndex];
                var firstProcessor = shifter.SignalProcessors[0];
                var lastProcessor = shifter.SignalProcessors[inputSignalCount - 1];

                AddConnection(CircuitColor.Green, shifter.InputSquared, CircuitId.Input, shifter.BufferedInput, CircuitId.Input);
                AddConnection(CircuitColor.Red, shifter.InputSquared, CircuitId.Output, shifter.NegativeInputSquared, CircuitId.Input);
                AddConnection(CircuitColor.Green, shifter.BufferedInput, CircuitId.Input, firstProcessor.InputBuffer, CircuitId.Input);
                AddConnection(CircuitColor.Green, shifter.BufferedInput, CircuitId.Output, firstProcessor.OutputCleaner, CircuitId.Input);
                AddConnection(CircuitColor.Green, shifter.NegativeInputSquared, CircuitId.Output, firstProcessor.OutputGenerator, CircuitId.Output);
                AddConnection(CircuitColor.Green, firstProcessor.OutputGenerator, CircuitId.Output, firstProcessor.OutputCleaner, CircuitId.Output);
                AddConnection(CircuitColor.Green, lastProcessor.InputChecker, CircuitId.Input, shifter.OutputMaps[0], null);
                AddConnection(CircuitColor.Green, shifter.OutputMaps[1], null, shifter.OutputMaps[0], null);

                // Input signal processor connections
                for (var processorIndex = 0; processorIndex < inputSignalCount; processorIndex++)
                {
                    var processor = shifter.SignalProcessors[processorIndex];

                    AddConnection(CircuitColor.Red, processor.InputChecker, CircuitId.Output, processor.InputBuffer, CircuitId.Output);
                    AddConnection(CircuitColor.Red, processor.InputBuffer, CircuitId.Output, processor.OutputGenerator, CircuitId.Input);

                    if (shifterIndex == 0)
                    {
                        var inputMap = inputMaps[processorIndex];

                        AddConnection(CircuitColor.Red, processor.InputChecker, CircuitId.Input, inputMap, null);
                    }
                    else
                    {
                        var adjacentShifter = shifters[shifterIndex - 1];
                        var adjacentProcessor = adjacentShifter.SignalProcessors[processorIndex];

                        AddConnection(CircuitColor.Red, processor.InputChecker, CircuitId.Input, adjacentProcessor.InputChecker, CircuitId.Input);
                    }

                    if (processorIndex > 0)
                    {
                        var adjacentProcessor = shifter.SignalProcessors[processorIndex - 1];

                        AddConnection(CircuitColor.Green, processor.InputChecker, CircuitId.Input, adjacentProcessor.InputChecker, CircuitId.Input);
                        AddConnection(CircuitColor.Green, processor.InputBuffer, CircuitId.Input, adjacentProcessor.InputBuffer, CircuitId.Input);
                        AddConnection(CircuitColor.Green, processor.OutputGenerator, CircuitId.Output, adjacentProcessor.OutputGenerator, CircuitId.Output);
                        AddConnection(CircuitColor.Green, processor.OutputCleaner, CircuitId.Input, adjacentProcessor.OutputCleaner, CircuitId.Input);
                        AddConnection(CircuitColor.Green, processor.OutputCleaner, CircuitId.Output, adjacentProcessor.OutputCleaner, CircuitId.Output);
                    }
                }

                // Output signal map connections
                for (var outputMapIndex = 2; outputMapIndex < shifter.OutputMaps.Length; outputMapIndex++)
                {
                    var outputMap = shifter.OutputMaps[outputMapIndex];
                    var adjacentOutputMap = shifter.OutputMaps[outputMapIndex - 2];

                    AddConnection(CircuitColor.Green, outputMap, null, adjacentOutputMap, null);
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

        private class Shifter
        {
            public Entity InputSquared { get; set; }
            public Entity BufferedInput { get; set; }
            public Entity NegativeInputSquared { get; set; }
            public SignalProcessor[] SignalProcessors { get; set; }
            public Entity[] OutputMaps { get; set; }
        }

        private class SignalProcessor
        {
            public Entity InputChecker { get; set; }
            public Entity InputBuffer { get; set; }
            public Entity OutputGenerator { get; set; }
            public Entity OutputCleaner { get; set; }
        }
    }

    public class SpriteShifterConfiguration
    {
        public int? SignalCount { get; init; }
    }
}
