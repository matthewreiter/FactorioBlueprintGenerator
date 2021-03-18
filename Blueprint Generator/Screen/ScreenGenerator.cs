using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using static BlueprintGenerator.ConnectionUtil;
using static BlueprintGenerator.PowerUtil;

namespace BlueprintGenerator.Screen
{
    public class ScreenGenerator : IBlueprintGenerator
    {
        public Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<ScreenConfiguration>());
        }

        public static Blueprint Generate(ScreenConfiguration configuration)
        {
            var width = configuration.Width ?? 18;
            var height = configuration.Height ?? 18;

            const int cycle = 2;
            const int parallelCycle = 32;

            var entities = new List<Entity>();
            var pixels = new Entity[height, width];
            var columnControllers = new Controller[width];
            var rowControllers = new Controller[height];

            // Pixels
            for (var row = 0; row < height; row++)
            {
                for (var column = 0; column < width; column++)
                {
                    var relativeRow = row % 18;
                    var relativeColumn = column % 18;

                    // Don't place lights that intersect the substations
                    if (relativeRow > 15 && relativeColumn > 15)
                    {
                        continue;
                    }

                    var pixel = new Entity
                    {
                        Name = ItemNames.Lamp,
                        Position = new Position
                        {
                            X = column + 2,
                            Y = row + 2
                        },
                        Control_behavior = new ControlBehavior
                        {
                            Circuit_condition = new CircuitCondition
                            {
                                First_signal = SignalID.Create(ScreenUtil.PixelSignals[row]),
                                Comparator = Comparators.GreaterThan,
                                Constant = 0
                            },
                            Use_colors = true
                        }
                    };
                    pixels[row, column] = pixel;
                    entities.Add(pixel);
                }
            }

            // Column controllers
            for (var column = 0; column < width; column++)
            {
                var controllerX = column + 2;
                var controllerY = height + 4;

                var memory = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = controllerX,
                        Y = controllerY + 0.5
                    },
                    Direction = Direction.Up,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Each),
                            Constant = 0,
                            Comparator = Comparators.GreaterThan,
                            Output_signal = SignalID.Create(VirtualSignalNames.Each),
                            Copy_count_from_input = false
                        }
                    }
                };
                entities.Add(memory);

                var writer = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = controllerX,
                        Y = controllerY + 2.5
                    },
                    Direction = Direction.Up,
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
                entities.Add(writer);

                var addressMatcher = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = controllerX,
                        Y = controllerY + 4.5
                    },
                    Direction = Direction.Up,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Info),
                            Constant = column + 1,
                            Comparator = Comparators.IsNotEqual,
                            Output_signal = SignalID.Create(VirtualSignalNames.Check),
                            Copy_count_from_input = false
                        }
                    }
                };
                entities.Add(addressMatcher);

                var cyclicWriter = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = controllerX,
                        Y = controllerY + 6.5
                    },
                    Direction = Direction.Up,
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
                entities.Add(cyclicWriter);

                var cyclicMatcher = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = controllerX,
                        Y = controllerY + 8.5
                    },
                    Direction = Direction.Up,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Info),
                            Constant = column % cycle + 1,
                            Comparator = Comparators.IsNotEqual,
                            Output_signal = SignalID.Create(VirtualSignalNames.Check),
                            Copy_count_from_input = false
                        }
                    }
                };
                entities.Add(cyclicMatcher);

                var parallelWriter = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = controllerX,
                        Y = controllerY + 14.5
                    },
                    Direction = Direction.Up,
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
                entities.Add(parallelWriter);

                var parallelAddressRangeLow = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = controllerX,
                        Y = controllerY + 10.5
                    },
                    Direction = Direction.Up,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Info),
                            Constant = column + 1,
                            Comparator = Comparators.LessThan,
                            Output_signal = SignalID.Create(VirtualSignalNames.Check),
                            Copy_count_from_input = false
                        }
                    }
                };
                entities.Add(parallelAddressRangeLow);

                var parallelAddressRangeHigh = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = controllerX,
                        Y = controllerY + 12.5
                    },
                    Direction = Direction.Up,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Info),
                            Constant = column + parallelCycle + 1,
                            Comparator = Comparators.GreaterThanOrEqualTo,
                            Output_signal = SignalID.Create(VirtualSignalNames.Check),
                            Copy_count_from_input = false
                        }
                    }
                };
                entities.Add(parallelAddressRangeHigh);

                var isOdd = column % 2 == 1;

                var parallelHorizontalLink1 = new Entity
                {
                    Name = column % 18 == 16 ? ItemNames.Substation : ItemNames.BigElectricPole,
                    Position = new Position
                    {
                        X = controllerX + 0.5,
                        Y = controllerY + 16.5 + (isOdd ? 2 : 0)
                    }
                };
                entities.Add(parallelHorizontalLink1);

                var parallelHorizontalLink2 = new Entity
                {
                    Name = ItemNames.BigElectricPole,
                    Position = new Position
                    {
                        X = controllerX + 0.5,
                        Y = controllerY + 20.5 + (isOdd ? 2 : 0)
                    }
                };
                entities.Add(parallelHorizontalLink2);

                var videoEnabler = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = controllerX,
                        Y = controllerY + 24.5
                    },
                    Direction = Direction.Up,
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
                entities.Add(videoEnabler);

                var videoReferenceSignalSubtractor = new Entity
                {
                    Name = ItemNames.ArithmeticCombinator,
                    Position = new Position
                    {
                        X = controllerX,
                        Y = controllerY + 26.5
                    },
                    Direction = Direction.Up,
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
                entities.Add(videoReferenceSignalSubtractor);

                var videoValueSpreader = new Entity
                {
                    Name = ItemNames.ArithmeticCombinator,
                    Position = new Position
                    {
                        X = controllerX,
                        Y = controllerY + 28.5
                    },
                    Direction = Direction.Up,
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
                entities.Add(videoValueSpreader);

                var videoBitIsolator = new Entity
                {
                    Name = ItemNames.ArithmeticCombinator,
                    Position = new Position
                    {
                        X = controllerX,
                        Y = controllerY + 30.5
                    },
                    Direction = Direction.Up,
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
                entities.Add(videoBitIsolator);

                var videoBitShifter = new Entity
                {
                    Name = ItemNames.ArithmeticCombinator,
                    Position = new Position
                    {
                        X = controllerX,
                        Y = controllerY + 32.5
                    },
                    Direction = Direction.Up,
                    Control_behavior = new ControlBehavior
                    {
                        Arithmetic_conditions = new ArithmeticConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Each),
                            Second_signal = SignalID.CreateLetterOrDigit('W'),
                            Operation = ArithmeticOperations.RightShift,
                            Output_signal = SignalID.Create(VirtualSignalNames.Each)
                        }
                    }
                };
                entities.Add(videoBitShifter);

                columnControllers[column] = new Controller
                {
                    Memory = memory,
                    Writer = writer,
                    AddressMatcher = addressMatcher,
                    Cyclic = new CyclicInput
                    {
                        Writer = cyclicWriter,
                        Matcher = cyclicMatcher
                    },
                    Parallel = new ParallelInput
                    {
                        Writer = parallelWriter,
                        AddressRangeLow = parallelAddressRangeLow,
                        AddressRangeHigh = parallelAddressRangeHigh,
                        HorizontalLink1 = parallelHorizontalLink1,
                        HorizontalLink2 = parallelHorizontalLink2
                    },
                    Video = new VideoInput
                    {
                        Enabler = videoEnabler,
                        ReferenceSignalSubtractor = videoReferenceSignalSubtractor,
                        ValueSpreader = videoValueSpreader,
                        BitIsolator = videoBitIsolator,
                        BitShifter = videoBitShifter
                    }
                };
            }

            // Row controllers
            for (var row = 0; row < height; row++)
            {
                var controllerY = row + 2;

                var memory = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = -1.5,
                        Y = controllerY
                    },
                    Direction = Direction.Right,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Each),
                            Constant = 0,
                            Comparator = Comparators.LessThan,
                            Output_signal = SignalID.Create(VirtualSignalNames.Each),
                            Copy_count_from_input = true
                        }
                    }
                };
                entities.Add(memory);

                var writer = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = -3.5,
                        Y = controllerY
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
                entities.Add(writer);

                var addressMatcher = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = -5.5,
                        Y = controllerY
                    },
                    Direction = Direction.Right,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Info),
                            Constant = row + 1,
                            Comparator = Comparators.IsNotEqual,
                            Output_signal = SignalID.Create(VirtualSignalNames.Check),
                            Copy_count_from_input = false
                        }
                    }
                };
                entities.Add(addressMatcher);

                var cyclicWriter = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = -7.5,
                        Y = controllerY
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
                entities.Add(cyclicWriter);

                var cyclicMatcher = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = -9.5,
                        Y = controllerY
                    },
                    Direction = Direction.Right,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Info),
                            Constant = row % cycle + 1,
                            Comparator = Comparators.IsNotEqual,
                            Output_signal = SignalID.Create(VirtualSignalNames.Check),
                            Copy_count_from_input = false
                        }
                    }
                };
                entities.Add(cyclicMatcher);

                rowControllers[row] = new Controller
                {
                    Memory = memory,
                    Writer = writer,
                    AddressMatcher = addressMatcher,
                    Cyclic = new CyclicInput
                    {
                        Writer = cyclicWriter,
                        Matcher = cyclicMatcher
                    }
                };
            }

            BlueprintUtil.PopulateEntityNumbers(entities);

            var substationWidth = (width + 8) / 18 + 1;
            var substationHeight = (height + 8) / 18 + 1;
            var substations = CreateSubstations(substationWidth, substationHeight, 0, 0, entities.Count + 1, GridConnectivity.Top | GridConnectivity.Vertical);
            entities.AddRange(substations);
            var substations2 = CreateSubstations(substationWidth, 1, 0, height + 38, entities.Count + 1);
            entities.AddRange(substations2);

            // Pixel connections
            for (var row = 0; row < height; row++)
            {
                for (var column = 0; column < width; column++)
                {
                    var pixel = pixels[row, column];
                    if (pixel == null)
                    {
                        continue;
                    }

                    static int GetAdjacency(int primaryAxis, int secondaryAxis)
                    {
                        var offset = primaryAxis % 18 == 0 && secondaryAxis % 18 > 15 ? 3 : 1;
                        return primaryAxis - offset;
                    }
 
                    var adjacentRow = GetAdjacency(row, column);
                    if (adjacentRow >= 0)
                    {
                        AddConnection(CircuitColor.Green, pixel, null, pixels[adjacentRow, column], null);
                    }

                    var adjacentColumn = GetAdjacency(column, row);
                    if (adjacentColumn >= 0)
                    {
                        AddConnection(CircuitColor.Red, pixel, null, pixels[row, adjacentColumn], null);
                    }
                }
            }

            // Column controller connections
            for (var column = 0; column < width; column++)
            {
                var pixel = pixels[height - 1, column];
                var controller = columnControllers[column];
                var isFirstHalfOfParallelCycle = column % parallelCycle < parallelCycle / 2;

                AddConnection(CircuitColor.Green, controller.Memory, CircuitId.Output, pixel, null); // Data out
                AddConnection(CircuitColor.Green, controller.Memory, CircuitId.Output, controller.Memory, CircuitId.Input); // Data feedback
                AddConnection(CircuitColor.Green, controller.Writer, CircuitId.Output, controller.Memory, CircuitId.Input); // Data transfer
                AddConnection(CircuitColor.Red, controller.AddressMatcher, CircuitId.Output, controller.Writer, CircuitId.Input); // Enable
                AddConnection(CircuitColor.Green, controller.Cyclic.Writer, CircuitId.Output, controller.Writer, CircuitId.Output); // Cyclic data transfer
                AddConnection(CircuitColor.Red, controller.Cyclic.Matcher, CircuitId.Output, controller.Cyclic.Writer, CircuitId.Input); // Cyclic enable
                AddConnection(CircuitColor.Green, controller.Parallel.Writer, CircuitId.Output, controller.Cyclic.Writer, CircuitId.Output); // Parallel data transfer
                AddConnection(CircuitColor.Red, controller.Parallel.AddressRangeLow, CircuitId.Output, controller.Parallel.Writer, CircuitId.Input); // Parallel enable low
                AddConnection(CircuitColor.Red, controller.Parallel.AddressRangeHigh, CircuitId.Output, controller.Parallel.AddressRangeLow, CircuitId.Output); // Parallel enable high
                AddConnection(CircuitColor.Red, controller.Parallel.AddressRangeHigh, CircuitId.Input, controller.Parallel.AddressRangeLow, CircuitId.Input); // Parallel address in
                AddConnection(CircuitColor.Green, isFirstHalfOfParallelCycle ? controller.Parallel.HorizontalLink1 : controller.Parallel.HorizontalLink2, null, controller.Parallel.Writer, CircuitId.Input); // Parallel data in
                AddConnection(CircuitColor.Green, controller.Video.Enabler, CircuitId.Output, controller.Parallel.Writer, CircuitId.Output); // Video data transfer
                AddConnection(CircuitColor.Green, controller.Video.ReferenceSignalSubtractor, CircuitId.Output, controller.Video.Enabler, CircuitId.Input); // Video data transfer
                AddConnection(CircuitColor.Green, controller.Video.ValueSpreader, CircuitId.Output, controller.Video.ReferenceSignalSubtractor, CircuitId.Output); // Video data transfer
                AddConnection(CircuitColor.Green, controller.Video.BitIsolator, CircuitId.Output, controller.Video.ValueSpreader, CircuitId.Input); // Video data transfer
                AddConnection(CircuitColor.Green, controller.Video.BitShifter, CircuitId.Output, controller.Video.BitIsolator, CircuitId.Input); // Video data transfer

                var adjacentColumn = column - 1;
                if (adjacentColumn >= 0)
                {
                    var adjacentController = columnControllers[adjacentColumn];

                    AddConnection(CircuitColor.Red, controller.Memory, CircuitId.Input, adjacentController.Memory, CircuitId.Input); // Full data in
                    AddConnection(CircuitColor.Green, controller.Writer, CircuitId.Input, adjacentController.Writer, CircuitId.Input); // Addressable data in
                    AddConnection(CircuitColor.Red, controller.AddressMatcher, CircuitId.Input, adjacentController.AddressMatcher, CircuitId.Input); // Address in
                    AddConnection(CircuitColor.Green, controller.Cyclic.Writer, CircuitId.Input, adjacentController.Cyclic.Writer, CircuitId.Input); // Cyclic data in
                    AddConnection(CircuitColor.Red, controller.Cyclic.Matcher, CircuitId.Input, adjacentController.Cyclic.Matcher, CircuitId.Input); // Cyclic address in
                    AddConnection(CircuitColor.Red, controller.Parallel.AddressRangeHigh, CircuitId.Input, adjacentController.Parallel.AddressRangeHigh, CircuitId.Input); // Parallel address in
                    AddConnection(CircuitColor.Red, controller.Video.Enabler, CircuitId.Input, adjacentController.Video.Enabler, CircuitId.Input); // Video enable
                    AddConnection(CircuitColor.Red, controller.Video.ReferenceSignalSubtractor, CircuitId.Input, adjacentController.Video.ReferenceSignalSubtractor, CircuitId.Input); // Video reference signal
                    AddConnection(CircuitColor.Red, controller.Video.ValueSpreader, CircuitId.Input, adjacentController.Video.ValueSpreader, CircuitId.Input); // Video reference signal
                    AddConnection(CircuitColor.Red, controller.Video.BitShifter, CircuitId.Input, adjacentController.Video.BitShifter, CircuitId.Input); // Video frame selector
                }

                var adjancentLinkColumn = column - parallelCycle / 2;
                if (adjancentLinkColumn >= 0)
                {
                    var previousCycleController = columnControllers[adjancentLinkColumn];

                    AddConnection(CircuitColor.Green, controller.Parallel.HorizontalLink1, null, previousCycleController.Parallel.HorizontalLink1, null); // Parallel data in
                    AddConnection(CircuitColor.Green, controller.Parallel.HorizontalLink2, null, previousCycleController.Parallel.HorizontalLink2, null); // Parallel data in
                }

                if (controller.Parallel.HorizontalLink1.Name == ItemNames.Substation)
                {
                    AddNeighbor(controller.Parallel.HorizontalLink1, substations[(substationHeight - 1) * substationWidth + column / 18 + 1]);
                    AddNeighbor(controller.Parallel.HorizontalLink1, substations2[column / 18 + 1]);
                }
            }

            // Row controller connections
            for (var row = 0; row < height; row++)
            {
                var pixel = pixels[row, 0];
                var controller = rowControllers[row];

                AddConnection(CircuitColor.Red, controller.Memory, CircuitId.Output, pixel, null); // Data out
                AddConnection(CircuitColor.Red, controller.Memory, CircuitId.Output, controller.Memory, CircuitId.Input); // Data feedback
                AddConnection(CircuitColor.Red, controller.Writer, CircuitId.Output, controller.Memory, CircuitId.Input); // Data transfer
                AddConnection(CircuitColor.Red, controller.AddressMatcher, CircuitId.Output, controller.Writer, CircuitId.Input); // Enable
                AddConnection(CircuitColor.Red, controller.Cyclic.Writer, CircuitId.Output, controller.Writer, CircuitId.Output); // Cyclic data transfer
                AddConnection(CircuitColor.Red, controller.Cyclic.Matcher, CircuitId.Output, controller.Cyclic.Writer, CircuitId.Input); // Cyclic enable

                var adjacentRow = row - 1;
                if (adjacentRow >= 0)
                {
                    var adjacentController = rowControllers[adjacentRow];

                    AddConnection(CircuitColor.Green, controller.Memory, CircuitId.Input, adjacentController.Memory, CircuitId.Input); // Full data in
                    AddConnection(CircuitColor.Green, controller.Writer, CircuitId.Input, adjacentController.Writer, CircuitId.Input); // Addressable data in
                    AddConnection(CircuitColor.Red, controller.AddressMatcher, CircuitId.Input, adjacentController.AddressMatcher, CircuitId.Input); // Address in
                    AddConnection(CircuitColor.Green, controller.Cyclic.Writer, CircuitId.Input, adjacentController.Cyclic.Writer, CircuitId.Input); // Cyclic data in
                    AddConnection(CircuitColor.Red, controller.Cyclic.Matcher, CircuitId.Input, adjacentController.Cyclic.Matcher, CircuitId.Input); // Cyclic address in
                }
            }

            return new Blueprint
            {
                Label = $"{width}x{height} Screen",
                Icons = new List<Icon>
                {
                    Icon.Create(ItemNames.Lamp)
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };
        }

        private class Controller
        {
            public Entity Memory { get; set; }
            public Entity Writer { get; set; }
            public Entity AddressMatcher { get; set; }
            public CyclicInput Cyclic { get; set; }
            public ParallelInput Parallel { get; set; }
            public VideoInput Video { get; set; }
        }

        private class CyclicInput
        {
            public Entity Writer { get; set; }
            public Entity Matcher { get; set; }
        }

        private class ParallelInput
        {
            public Entity Writer { get; set; }
            public Entity AddressRangeLow { get; set; }
            public Entity AddressRangeHigh { get; set; }
            public Entity HorizontalLink1 { get; set; }
            public Entity HorizontalLink2 { get; set; }
        }

        private class VideoInput
        {
            public Entity Enabler { get; set; }
            public Entity ReferenceSignalSubtractor { get; set; }
            public Entity ValueSpreader { get; set; }
            public Entity BitIsolator { get; set; }
            public Entity BitShifter { get; set; }
        }
    }

    public class ScreenConfiguration
    {
        /// <summary>
        /// The width of the screen, in pixels.
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// The height of the screen, in pixels.
        /// </summary>
        public int? Height { get; set; }
    }
}
