using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
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

            var entities = new List<Entity>();
            var pixels = new Entity[height, width];

            var columnMemory = new Entity[width];
            var columnWriters = new Entity[width];
            var columnAddressMatchers = new Entity[width];
            var columnCyclicWriters = new Entity[width];
            var columnCyclicMatchers = new Entity[width];

            var rowMemory = new Entity[height];
            var rowWriters = new Entity[height];
            var rowAddressMatchers = new Entity[height];
            var rowCyclicWriters = new Entity[height];
            var rowCyclicMatchers = new Entity[height];

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
                columnMemory[column] = memory;
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
                columnWriters[column] = writer;
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
                columnAddressMatchers[column] = addressMatcher;
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
                columnCyclicWriters[column] = cyclicWriter;
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
                columnCyclicMatchers[column] = cyclicMatcher;
                entities.Add(cyclicMatcher);
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
                rowMemory[row] = memory;
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
                rowWriters[row] = writer;
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
                rowAddressMatchers[row] = addressMatcher;
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
                rowCyclicWriters[row] = cyclicWriter;
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
                rowCyclicMatchers[row] = cyclicMatcher;
                entities.Add(cyclicMatcher);
            }

            BlueprintUtil.PopulateEntityNumbers(entities);

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
                var memory = columnMemory[column];
                var writer = columnWriters[column];
                var addressMatcher = columnAddressMatchers[column];
                var cyclicWriter = columnCyclicWriters[column];
                var cyclicMatcher = columnCyclicMatchers[column];

                AddConnection(CircuitColor.Green, memory, CircuitId.Output, pixel, null); // Data out
                AddConnection(CircuitColor.Green, memory, CircuitId.Output, memory, CircuitId.Input); // Data feedback
                AddConnection(CircuitColor.Green, writer, CircuitId.Output, memory, CircuitId.Input); // Data transfer
                AddConnection(CircuitColor.Red, addressMatcher, CircuitId.Output, writer, CircuitId.Input); // Enable
                AddConnection(CircuitColor.Red, cyclicWriter, CircuitId.Output, writer, CircuitId.Output); // Cyclic data transfer
                AddConnection(CircuitColor.Red, cyclicMatcher, CircuitId.Output, cyclicWriter, CircuitId.Input); // Cyclic enable

                var adjacentColumn = column - 1;
                if (adjacentColumn >= 0)
                {
                    var adjacentMemory = columnMemory[adjacentColumn];
                    var adjacentWriter = columnWriters[adjacentColumn];
                    var adjacentAddressMatcher = columnAddressMatchers[adjacentColumn];
                    var adjacentCyclicWriter = columnCyclicWriters[adjacentColumn];
                    var adjacentCyclicMatcher = columnCyclicMatchers[adjacentColumn];

                    AddConnection(CircuitColor.Red, memory, CircuitId.Input, adjacentMemory, CircuitId.Input); // Full data in
                    AddConnection(CircuitColor.Green, writer, CircuitId.Input, adjacentWriter, CircuitId.Input); // Addressable data in
                    AddConnection(CircuitColor.Red, addressMatcher, CircuitId.Input, adjacentAddressMatcher, CircuitId.Input); // Address in
                    AddConnection(CircuitColor.Green, cyclicWriter, CircuitId.Input, adjacentCyclicWriter, CircuitId.Input); // Cyclic data in
                    AddConnection(CircuitColor.Red, cyclicMatcher, CircuitId.Input, adjacentCyclicMatcher, CircuitId.Input); // Cyclic address in
                }
            }

            // Row controller connections
            for (var row = 0; row < height; row++)
            {
                var pixel = pixels[row, 0];
                var memory = rowMemory[row];
                var writer = rowWriters[row];
                var addressMatcher = rowAddressMatchers[row];
                var cyclicWriter = rowCyclicWriters[row];
                var cyclicMatcher = rowCyclicMatchers[row];

                AddConnection(CircuitColor.Red, memory, CircuitId.Output, pixel, null); // Data out
                AddConnection(CircuitColor.Red, memory, CircuitId.Output, memory, CircuitId.Input); // Data feedback
                AddConnection(CircuitColor.Red, writer, CircuitId.Output, memory, CircuitId.Input); // Data transfer
                AddConnection(CircuitColor.Red, addressMatcher, CircuitId.Output, writer, CircuitId.Input); // Enable
                AddConnection(CircuitColor.Red, cyclicWriter, CircuitId.Output, writer, CircuitId.Output); // Cyclic data transfer
                AddConnection(CircuitColor.Red, cyclicMatcher, CircuitId.Output, cyclicWriter, CircuitId.Input); // Cyclic enable

                var adjacentRow = row - 1;
                if (adjacentRow >= 0)
                {
                    var adjacentMemory = rowMemory[adjacentRow];
                    var adjacentWriter = rowWriters[adjacentRow];
                    var adjacentAddressMatcher = rowAddressMatchers[adjacentRow];
                    var adjacentCyclicWriter = rowCyclicWriters[adjacentRow];
                    var adjacentCyclicMatcher = rowCyclicMatchers[adjacentRow];

                    AddConnection(CircuitColor.Green, memory, CircuitId.Input, adjacentMemory, CircuitId.Input); // Full data in
                    AddConnection(CircuitColor.Green, writer, CircuitId.Input, adjacentWriter, CircuitId.Input); // Addressable data in
                    AddConnection(CircuitColor.Red, addressMatcher, CircuitId.Input, adjacentAddressMatcher, CircuitId.Input); // Address in
                    AddConnection(CircuitColor.Green, cyclicWriter, CircuitId.Input, adjacentCyclicWriter, CircuitId.Input); // Cyclic data in
                    AddConnection(CircuitColor.Red, cyclicMatcher, CircuitId.Input, adjacentCyclicMatcher, CircuitId.Input); // Cyclic address in
                }
            }

            var substationWidth = (width + 8) / 18 + 1;
            var substationHeight = (height + 8) / 18 + 1;

            entities.AddRange(CreateSubstations(substationWidth, substationHeight, 0, 0, entities.Count + 1, GridConnectivity.Top | GridConnectivity.Vertical));

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
