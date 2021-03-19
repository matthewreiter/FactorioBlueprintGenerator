using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using static BlueprintGenerator.ConnectionUtil;
using static BlueprintGenerator.PowerUtil;

namespace BlueprintGenerator.Screen
{
    public class VideoRomGenerator : IBlueprintGenerator
    {
        public Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<VideoMemoryConfiguration>());
        }

        public static Blueprint Generate(VideoMemoryConfiguration configuration, List<bool[,]> frames = null)
        {
            var width = configuration.Width ?? 32;
            var height = configuration.Height ?? 2;
            var baseAddress = configuration.BaseAddress ?? 1;

            var frameHeight = frames?.ElementAtOrDefault(0)?.GetLength(0) ?? 0;

            const int framesPerRow = 32;
            const int maxFilters = 20;

            var entities = new List<Entity>();
            var memoryRows = new MemoryRow[height];

            var metadata = new Entity
            {
                Name = ItemNames.ConstantCombinator,
                Position = new Position
                {
                    X = 2,
                    Y = 0
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Filters = new List<Filter>
                    {
                        Filter.Create(VirtualSignalNames.LetterOrDigit('Z'), frames?.Count ?? 0)
                    }
                }
            };
            entities.Add(metadata);

            for (var row = 0; row < height; row++)
            {
                var cellY = row * 9 - (row % 2 == 1 ? 1 : 0) + 2;
                var rowFrames = frames?.Skip(row * framesPerRow).Take(framesPerRow).ToList();

                var addressMatcher = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = 1,
                        Y = cellY + 0.5
                    },
                    Direction = Direction.Down,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Info),
                            Constant = row + baseAddress,
                            Comparator = Comparators.IsNotEqual,
                            Output_signal = SignalID.Create(VirtualSignalNames.Check),
                            Copy_count_from_input = false
                        }
                    }
                };
                entities.Add(addressMatcher);

                var memoryCells = new MemoryCell[width];

                for (var column = 0; column < width; column++)
                {
                    var cellX = column + 2;

                    var enabler = new Entity
                    {
                        Name = ItemNames.DeciderCombinator,
                        Position = new Position
                        {
                            X = cellX,
                            Y = cellY + 0.5
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
                    entities.Add(enabler);

                    var pixelFilters = Enumerable.Range(0, frameHeight)
                        .Select(frameRow =>
                        {
                            var pixel = rowFrames
                                .Select((frame, frameOffset) => frame[frameRow, column] ? 1 << frameOffset : 0)
                                .Sum();

                            return Filter.Create(ScreenUtil.PixelSignals[frameRow], pixel);
                        })
                        .Where(pixelFilter => pixelFilter.Count != 0)
                        .ToList();

                    var subCellCount = column % 18 >= 16 ? 6 : 7;
                    var subCells = new Entity[subCellCount];

                    for (var subCellIndex = 0; subCellIndex < subCellCount; subCellIndex++)
                    {
                        var subCell = new Entity
                        {
                            Name = ItemNames.ConstantCombinator,
                            Position = new Position
                            {
                                X = cellX,
                                Y = subCellIndex < 6 || row % 2 == 1 ? cellY + subCellIndex + 2 : cellY - 1
                            },
                            Direction = Direction.Down,
                            Control_behavior = new ControlBehavior
                            {
                                Filters = pixelFilters.Skip(subCellIndex * maxFilters).Take(maxFilters).ToList()
                            }
                        };
                        entities.Add(subCell);
                        subCells[subCellIndex] = subCell;
                    }

                    memoryCells[column] = new MemoryCell
                    {
                        Enabler = enabler,
                        SubCells = subCells
                    };
                }

                memoryRows[row] = new MemoryRow
                {
                    AddressMatcher = addressMatcher,
                    Cells = memoryCells
                };
            }

            BlueprintUtil.PopulateEntityNumbers(entities);

            var substationWidth = (width + 9) / 18 + 1;
            var substationHeight = height / 2 + 1;

            entities.AddRange(CreateSubstations(substationWidth, substationHeight, 0, 0, entities.Count + 1));

            for (var row = 0; row < height; row++)
            {
                var memoryRow = memoryRows[row];

                AddConnection(CircuitColor.Red, memoryRow.AddressMatcher, CircuitId.Output, memoryRow.Cells[0].Enabler, CircuitId.Input);

                var adjacentRow = row - 1;
                if (adjacentRow >= 0)
                {
                    var adjacentMemoryRow = memoryRows[adjacentRow];

                    AddConnection(CircuitColor.Green, memoryRow.AddressMatcher, CircuitId.Input, adjacentMemoryRow.AddressMatcher, CircuitId.Input);

                    for (var column = 0; column < width; column++)
                    {
                        var memoryCell = memoryRow.Cells[column];
                        var adjacentMemoryCell = adjacentMemoryRow.Cells[column];

                        AddConnection(CircuitColor.Green, memoryCell.Enabler, CircuitId.Output, adjacentMemoryCell.Enabler, CircuitId.Output);
                    }
                }

                for (var column = 0; column < width; column++)
                {
                    var memoryCell = memoryRow.Cells[column];

                    AddConnection(CircuitColor.Green, memoryCell.SubCells[0], null, memoryCell.Enabler, CircuitId.Input);

                    for (var subCellIndex = 1; subCellIndex < memoryCell.SubCells.Length; subCellIndex++)
                    {
                        var subCell = memoryCell.SubCells[subCellIndex];
                        var adjacentSubCell = memoryCell.SubCells[subCellIndex < 6 || row % 2 == 1 ? subCellIndex - 1 : 0];

                        AddConnection(CircuitColor.Green, subCell, null, adjacentSubCell, null);
                    }

                    var adjacentColumn = column - 1;
                    if (adjacentColumn >= 0)
                    {
                        var adjacentMemoryCell = memoryRow.Cells[adjacentColumn];

                        AddConnection(CircuitColor.Red, memoryCell.Enabler, CircuitId.Input, adjacentMemoryCell.Enabler, CircuitId.Input);
                    }
                }
            }

            return new Blueprint
            {
                Label = $"Video ROM",
                Icons = new List<Icon>
                {
                    Icon.Create(ItemNames.Lamp),
                    Icon.Create(ItemNames.ConstantCombinator)
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };
        }

        private class MemoryRow
        {
            public Entity AddressMatcher { get; set; }
            public MemoryCell[] Cells { get; set; }
        }

        private class MemoryCell
        {
            public Entity Enabler { get; set; }
            public Entity[] SubCells { get; set; }
        }
    }

    public class VideoMemoryConfiguration
    {
        /// <summary>
        /// Whether the blueprint should snap to the grid based on the X and Y offsets.
        /// </summary>
        public bool? SnapToGrid { get; set; }

        /// <summary>
        /// The X offset of the blueprint.
        /// </summary>
        public int? X { get; set; }

        /// <summary>
        /// The Y offset of the blueprint.
        /// </summary>
        public int? Y { get; set; }

        /// <summary>
        /// The width of the ROM, in cells.
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// The height of the ROM, in cells.
        /// </summary>
        public int? Height { get; set; }

        /// <summary>
        /// The base address for the video frames.
        /// </summary>
        public int? BaseAddress { get; set; }
    }
}
