using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using static MemoryInitializer.ConnectionUtil;
using static MemoryInitializer.PowerUtil;

namespace MemoryInitializer.Screen
{
    public static class ScreenGenerator
    {
        public static Blueprint Generate(IConfigurationRoot configuration)
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
                // Memory
                var memory = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = column + 2,
                        Y = height + 4.5
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

                // Writer
                var writer = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = column + 2,
                        Y = height + 6.5
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

                // Address matcher
                var addressMatcher = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = column + 2,
                        Y = height + 8.5
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

                // Cyclic writer
                var cyclicWriter = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = column + 2,
                        Y = height + 10.5
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

                // Cyclic address matcher
                var cyclicMatcher = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = column + 2,
                        Y = height + 12.5
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

                // Parallel writer
                var parallelWriter = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = column + 2,
                        Y = height + 18.5
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

                // Parallel address range low
                var parallelAddressRangeLow = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = column + 2,
                        Y = height + 14.5
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

                // Parallel address range high
                var parallelAddressRangeHigh = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = column + 2,
                        Y = height + 16.5
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

                // Horizontal link 1
                var horizontalLink1 = new Entity
                {
                    Name = column % 18 == 16 ? ItemNames.Substation : ItemNames.BigElectricPole,
                    Position = new Position
                    {
                        X = column + 2.5,
                        Y = height + 20.5 + (isOdd ? 2 : 0)
                    }
                };
                entities.Add(horizontalLink1);

                // Horizontal link 2
                var horizontalLink2 = new Entity
                {
                    Name = ItemNames.BigElectricPole,
                    Position = new Position
                    {
                        X = column + 2.5,
                        Y = height + 24.5 + (isOdd ? 2 : 0)
                    }
                };
                entities.Add(horizontalLink2);

                columnControllers[column] = new Controller
                {
                    Memory = memory,
                    Writer = writer,
                    AddressMatcher = addressMatcher,
                    CyclicWriter = cyclicWriter,
                    CyclicMatcher = cyclicMatcher,
                    ParallelWriter = parallelWriter,
                    ParallelAddressRangeLow = parallelAddressRangeLow,
                    ParallelAddressRangeHigh = parallelAddressRangeHigh,
                    HorizontalLink1 = horizontalLink1,
                    HorizontalLink2 = horizontalLink2
                };
            }

            // Row controllers
            for (var row = 0; row < height; row++)
            {
                // Memory
                var memory = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = -1.5,
                        Y = row + 2
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
                            Copy_count_from_input = false
                        }
                    }
                };
                entities.Add(memory);

                // Writer
                var writer = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = -3.5,
                        Y = row + 2
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

                // Address matcher
                var addressMatcher = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = -5.5,
                        Y = row + 2
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

                // Cyclic Writer
                var cyclicWriter = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = -7.5,
                        Y = row + 2
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

                // Cyclic matcher
                var cyclicMatcher = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = -9.5,
                        Y = row + 2
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
                    CyclicWriter = cyclicWriter,
                    CyclicMatcher = cyclicMatcher
                };
            }

            BlueprintUtil.PopulateEntityNumbers(entities);

            var substationWidth = (width + 8) / 18 + 1;
            var substationHeight = (height + 8) / 18 + 1;
            var substations = CreateSubstations(substationWidth, substationHeight, 0, 0, entities.Count + 1, GridConnectivity.Top | GridConnectivity.Vertical);
            entities.AddRange(substations);

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
                AddConnection(CircuitColor.Green, controller.CyclicWriter, CircuitId.Output, controller.Writer, CircuitId.Output); // Cyclic data transfer
                AddConnection(CircuitColor.Red, controller.CyclicMatcher, CircuitId.Output, controller.CyclicWriter, CircuitId.Input); // Cyclic enable
                AddConnection(CircuitColor.Green, controller.ParallelWriter, CircuitId.Output, controller.CyclicWriter, CircuitId.Output); // Parallel data transfer
                AddConnection(CircuitColor.Red, controller.ParallelAddressRangeLow, CircuitId.Output, controller.ParallelWriter, CircuitId.Input); // Parallel enable low
                AddConnection(CircuitColor.Red, controller.ParallelAddressRangeHigh, CircuitId.Output, controller.ParallelAddressRangeLow, CircuitId.Output); // Parallel enable high
                AddConnection(CircuitColor.Red, controller.ParallelAddressRangeHigh, CircuitId.Input, controller.ParallelAddressRangeLow, CircuitId.Input); // Parallel address in
                AddConnection(CircuitColor.Green, isFirstHalfOfParallelCycle ? controller.HorizontalLink1 : controller.HorizontalLink2, null, controller.ParallelWriter, CircuitId.Input); // Parallel data in

                var adjacentColumn = column - 1;
                if (adjacentColumn >= 0)
                {
                    var adjacentController = columnControllers[adjacentColumn];

                    AddConnection(CircuitColor.Red, controller.Memory, CircuitId.Input, adjacentController.Memory, CircuitId.Input); // Full data in
                    AddConnection(CircuitColor.Green, controller.Writer, CircuitId.Input, adjacentController.Writer, CircuitId.Input); // Addressable data in
                    AddConnection(CircuitColor.Red, controller.AddressMatcher, CircuitId.Input, adjacentController.AddressMatcher, CircuitId.Input); // Address in
                    AddConnection(CircuitColor.Green, controller.CyclicWriter, CircuitId.Input, adjacentController.CyclicWriter, CircuitId.Input); // Cyclic data in
                    AddConnection(CircuitColor.Red, controller.CyclicMatcher, CircuitId.Input, adjacentController.CyclicMatcher, CircuitId.Input); // Cyclic address in
                    AddConnection(CircuitColor.Red, controller.ParallelAddressRangeHigh, CircuitId.Input, adjacentController.ParallelAddressRangeHigh, CircuitId.Input); // Parallel address in
                }

                var adjancentLinkColumn = column - parallelCycle / 2;
                if (adjancentLinkColumn >= 0)
                {
                    var previousCycleController = columnControllers[adjancentLinkColumn];

                    AddConnection(CircuitColor.Green, controller.HorizontalLink1, null, previousCycleController.HorizontalLink1, null); // Parallel data in
                    AddConnection(CircuitColor.Green, controller.HorizontalLink2, null, previousCycleController.HorizontalLink2, null); // Parallel data in
                }

                if (controller.HorizontalLink1.Name == ItemNames.Substation)
                {
                    AddNeighbor(controller.HorizontalLink1, substations[(substationHeight - 1) * substationWidth + column / 18 + 1]);
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
                AddConnection(CircuitColor.Red, controller.CyclicWriter, CircuitId.Output, controller.Writer, CircuitId.Output); // Cyclic data transfer
                AddConnection(CircuitColor.Red, controller.CyclicMatcher, CircuitId.Output, controller.CyclicWriter, CircuitId.Input); // Cyclic enable

                var adjacentRow = row - 1;
                if (adjacentRow >= 0)
                {
                    var adjacentController = rowControllers[adjacentRow];

                    AddConnection(CircuitColor.Green, controller.Memory, CircuitId.Input, adjacentController.Memory, CircuitId.Input); // Full data in
                    AddConnection(CircuitColor.Green, controller.Writer, CircuitId.Input, adjacentController.Writer, CircuitId.Input); // Addressable data in
                    AddConnection(CircuitColor.Red, controller.AddressMatcher, CircuitId.Input, adjacentController.AddressMatcher, CircuitId.Input); // Address in
                    AddConnection(CircuitColor.Green, controller.CyclicWriter, CircuitId.Input, adjacentController.CyclicWriter, CircuitId.Input); // Cyclic data in
                    AddConnection(CircuitColor.Red, controller.CyclicMatcher, CircuitId.Input, adjacentController.CyclicMatcher, CircuitId.Input); // Cyclic address in
                }
            }

            return new Blueprint
            {
                Label = $"{width}x{height} Screen",
                Icons = new List<Icon>
                {
                    new Icon
                    {
                        Signal = SignalID.Create(ItemNames.Lamp)
                    }
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
            public Entity CyclicWriter { get; set; }
            public Entity CyclicMatcher { get; set; }
            public Entity ParallelWriter { get; set; }
            public Entity ParallelAddressRangeLow { get; set; }
            public Entity ParallelAddressRangeHigh { get; set; }
            public Entity HorizontalLink1 { get; set; }
            public Entity HorizontalLink2 { get; set; }
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
