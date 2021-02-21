using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using MemoryInitializer.Constants;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using static MemoryInitializer.ConnectionUtil;
using static MemoryInitializer.PowerUtil;

namespace MemoryInitializer.Screen
{
    public class SpriteShifterGenerator : IBlueprintGenerator
    {
        public Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<SpriteShifterConfiguration>());
        }

        public static Blueprint Generate(SpriteShifterConfiguration configuration)
        {
            var signalCount = configuration.SignalCount ?? ScreenUtil.PixelSignals.Count;

            const int maxFilters = 20;
            const int shifterCount = 32;
            const int inputSignalCount = 32;

            var inputSignals = ComputerSignals.OrderedSignals.Take(inputSignalCount).ToList();
            var offsetSignal = VirtualSignalNames.Dot;

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
                        Filters = new List<Filter> { Filter.Create(offsetSignal, processorIndex + 1) }
                    }
                };
                inputMaps[processorIndex] = inputMap;
                entities.Add(inputMap);
            }

            // Shifters
            for (var shifterIndex = 0; shifterIndex < shifterCount; shifterIndex++)
            {
                var shifterX = shifterIndex * 2 + shifterIndex / 8 * 2 + 2;

                var outputLink = new Entity
                {
                    Name = ItemNames.BigElectricPole,
                    Position = new Position
                    {
                        X = 0.5 + shifterX,
                        Y = -1.5
                    }
                };
                entities.Add(outputLink);

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
                                Second_signal = SignalID.Create(offsetSignal),
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
                                Output_signal = SignalID.Create(offsetSignal)
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

                var offsetBuffer = new Entity
                {
                    Name = ItemNames.ArithmeticCombinator,
                    Position = new Position
                    {
                        X = 0.5 + shifterX,
                        Y = outputMaps[^1].Position.Y + 1
                    },
                    Direction = Direction.Right,
                    Control_behavior = new ControlBehavior
                    {
                        Arithmetic_conditions = new ArithmeticConditions
                        {
                            First_signal = SignalID.Create(offsetSignal),
                            Second_constant = 1,
                            Operation = ArithmeticOperations.Multiplication,
                            Output_signal = SignalID.Create(offsetSignal)
                        }
                    }
                };
                entities.Add(offsetBuffer);

                var horizontalShifter = CreateHorizontalShifter(shifterIndex, shifterX);
                entities.Add(horizontalShifter.BitShifter);
                entities.Add(horizontalShifter.BitIsolator);
                entities.Add(horizontalShifter.ValueSpreader);
                entities.Add(horizontalShifter.ReferenceSignalSubtractor);
                entities.Add(horizontalShifter.MaskChecker);
                entities.Add(horizontalShifter.Mask);

                shifters[shifterIndex] = new Shifter
                {
                    HorizontalShifter = horizontalShifter,
                    OutputLink = outputLink,
                    InputSquared = inputSquared,
                    BufferedInput = bufferedInput,
                    NegativeInputSquared = negativeInputSquared,
                    SignalProcessors = signalProcessors,
                    OutputMaps = outputMaps,
                    OffsetBuffer = offsetBuffer
                };
            }

            BlueprintUtil.PopulateEntityNumbers(entities);

            var substationWidth = shifterCount / 8 + 1;
            var substationHeight = (inputSignalCount * 4 + 3) / 18 + 2;
            entities.AddRange(CreateSubstations(substationWidth, substationHeight, 0, -10, entities.Count + 1));

            for (var shifterIndex = 0; shifterIndex < shifters.Length; shifterIndex++)
            {
                var shifter = shifters[shifterIndex];
                var firstProcessor = shifter.SignalProcessors[0];
                var lastProcessor = shifter.SignalProcessors[inputSignalCount - 1];
                var adjacentShifter = shifterIndex > 0 ? shifters[shifterIndex - 1] : null;

                AddConnection(CircuitColor.Green, shifter.NegativeInputSquared, CircuitId.Output, shifter.OutputLink, null);
                AddConnection(CircuitColor.Green, shifter.InputSquared, CircuitId.Input, shifter.BufferedInput, CircuitId.Input);
                AddConnection(CircuitColor.Red, shifter.InputSquared, CircuitId.Output, shifter.NegativeInputSquared, CircuitId.Input);
                AddConnection(CircuitColor.Green, shifter.BufferedInput, CircuitId.Input, firstProcessor.InputBuffer, CircuitId.Input);
                AddConnection(CircuitColor.Green, shifter.BufferedInput, CircuitId.Output, firstProcessor.OutputCleaner, CircuitId.Input);
                AddConnection(CircuitColor.Green, shifter.NegativeInputSquared, CircuitId.Output, firstProcessor.OutputGenerator, CircuitId.Output);
                AddConnection(CircuitColor.Green, firstProcessor.OutputGenerator, CircuitId.Output, firstProcessor.OutputCleaner, CircuitId.Output);
                AddConnection(CircuitColor.Green, lastProcessor.InputChecker, CircuitId.Input, shifter.OutputMaps[0], null);
                AddConnection(CircuitColor.Green, shifter.OutputMaps[1], null, shifter.OutputMaps[0], null);
                AddConnection(CircuitColor.Green, shifter.OffsetBuffer, CircuitId.Output, shifter.OutputMaps[^1], null);

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

                if (adjacentShifter != null)
                {
                    AddConnection(CircuitColor.Green, shifter.OffsetBuffer, CircuitId.Input, adjacentShifter.OffsetBuffer, CircuitId.Input);
                }

                AddHorizontalShifterConnections(shifter.HorizontalShifter, adjacentShifter?.HorizontalShifter);
                AddConnection(CircuitColor.Green, shifter.HorizontalShifter.Mask, CircuitId.Output, shifter.InputSquared, CircuitId.Input);
            }

            return new Blueprint
            {
                Label = $"Sprite Shifter",
                Icons = new List<Icon>
                {
                    Icon.Create(ItemNames.DeciderCombinator),
                    Icon.Create(ItemNames.DeciderCombinator)
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };
        }

        private static HorizontalShifter CreateHorizontalShifter(int shifterIndex, double shifterX)
        {
            var maskSignal = VirtualSignalNames.LetterOrDigit('W');

            var bitShifter = new Entity
            {
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = 0.5 + shifterX,
                    Y = -14
                },
                Direction = Direction.Right,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new ArithmeticConditions
                    {
                        First_signal = SignalID.Create(VirtualSignalNames.Each),
                        Second_constant = shifterIndex,
                        Operation = ArithmeticOperations.RightShift,
                        Output_signal = SignalID.Create(VirtualSignalNames.Each)
                    }
                }
            };

            var bitIsolator = new Entity
            {
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = 0.5 + shifterX,
                    Y = -13
                },
                Direction = Direction.Right,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new ArithmeticConditions
                    {
                        First_signal = SignalID.Create(VirtualSignalNames.Each),
                        Second_constant = 1,
                        Operation = ArithmeticOperations.And,
                        Output_signal = SignalID.Create(VirtualSignalNames.Each)
                    }
                }
            };

            var valueSpreader = new Entity
            {
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = 0.5 + shifterX,
                    Y = -12
                },
                Direction = Direction.Right,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new ArithmeticConditions
                    {
                        First_signal = SignalID.Create(VirtualSignalNames.Each),
                        Second_constant = 2,
                        Operation = ArithmeticOperations.Multiplication,
                        Output_signal = SignalID.Create(VirtualSignalNames.Each)
                    }
                }
            };

            var referenceSignalSubtractor = new Entity
            {
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = 0.5 + shifterX,
                    Y = -11
                },
                Direction = Direction.Right,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new ArithmeticConditions
                    {
                        First_signal = SignalID.Create(VirtualSignalNames.Each),
                        Second_constant = -3,
                        Operation = ArithmeticOperations.Multiplication,
                        Output_signal = SignalID.Create(VirtualSignalNames.Each)
                    }
                }
            };

            var maskChecker = new Entity
            {
                Name = ItemNames.DeciderCombinator,
                Position = new Position
                {
                    X = 0.5 + shifterX,
                    Y = -10
                },
                Direction = Direction.Right,
                Control_behavior = new ControlBehavior
                {
                    Decider_conditions = new DeciderConditions
                    {
                        First_signal = SignalID.Create(maskSignal),
                        Constant = 0,
                        Comparator = Comparators.IsEqual,
                        Output_signal = SignalID.Create(VirtualSignalNames.Check),
                        Copy_count_from_input = false
                    }
                }
            };

            var mask = new Entity
            {
                Name = ItemNames.DeciderCombinator,
                Position = new Position
                {
                    X = 0.5 + shifterX,
                    Y = -9
                },
                Direction = Direction.Right,
                Control_behavior = new ControlBehavior
                {
                    Decider_conditions = new DeciderConditions
                    {
                        First_signal = SignalID.Create(VirtualSignalNames.Check),
                        Constant = 0,
                        Comparator = Comparators.IsEqual,
                        Output_signal = SignalID.Create(VirtualSignalNames.Everything),
                        Copy_count_from_input = true
                    }
                }
            };

            return new HorizontalShifter
            {
                BitShifter = bitShifter,
                BitIsolator = bitIsolator,
                ValueSpreader = valueSpreader,
                ReferenceSignalSubtractor = referenceSignalSubtractor,
                MaskChecker = maskChecker,
                Mask = mask
            };
        }

        private static void AddHorizontalShifterConnections(HorizontalShifter horizontalShifter, HorizontalShifter adjacentHorizontalShifter)
        {
            AddConnection(CircuitColor.Green, horizontalShifter.BitShifter, CircuitId.Output, horizontalShifter.BitIsolator, CircuitId.Input);
            AddConnection(CircuitColor.Green, horizontalShifter.BitIsolator, CircuitId.Output, horizontalShifter.ValueSpreader, CircuitId.Input);
            AddConnection(CircuitColor.Green, horizontalShifter.ValueSpreader, CircuitId.Output, horizontalShifter.ReferenceSignalSubtractor, CircuitId.Output);
            AddConnection(CircuitColor.Green, horizontalShifter.ReferenceSignalSubtractor, CircuitId.Output, horizontalShifter.Mask, CircuitId.Input);
            AddConnection(CircuitColor.Green, horizontalShifter.ValueSpreader, CircuitId.Input, horizontalShifter.MaskChecker, CircuitId.Input);
            AddConnection(CircuitColor.Red, horizontalShifter.MaskChecker, CircuitId.Output, horizontalShifter.Mask, CircuitId.Input);

            if (adjacentHorizontalShifter != null)
            {
                AddConnection(CircuitColor.Green, horizontalShifter.BitShifter, CircuitId.Input, adjacentHorizontalShifter.BitShifter, CircuitId.Input);
                AddConnection(CircuitColor.Red, horizontalShifter.ValueSpreader, CircuitId.Input, adjacentHorizontalShifter.ValueSpreader, CircuitId.Input);
                AddConnection(CircuitColor.Red, horizontalShifter.ReferenceSignalSubtractor, CircuitId.Input, adjacentHorizontalShifter.ReferenceSignalSubtractor, CircuitId.Input);
            }
        }

        private class Shifter
        {
            public HorizontalShifter HorizontalShifter { get; set; }
            public Entity OutputLink { get; set; }
            public Entity InputSquared { get; set; }
            public Entity BufferedInput { get; set; }
            public Entity NegativeInputSquared { get; set; }
            public SignalProcessor[] SignalProcessors { get; set; }
            public Entity[] OutputMaps { get; set; }
            public Entity OffsetBuffer { get; set; }
        }

        private class HorizontalShifter
        {
            public Entity BitShifter { get; set; }
            public Entity BitIsolator { get; set; }
            public Entity ValueSpreader { get; set; }
            public Entity ReferenceSignalSubtractor { get; set; }
            public Entity MaskChecker { get; set; }
            public Entity Mask { get; set; }
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
