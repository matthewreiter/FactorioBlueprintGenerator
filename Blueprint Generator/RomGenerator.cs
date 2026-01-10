using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueprintGenerator;

public class RomGenerator : IBlueprintGenerator
{
    public Blueprint Generate(IConfigurationRoot configuration)
    {
        return Generate(configuration.Get<RomConfiguration>());
    }

    public static Blueprint Generate(RomConfiguration configuration, IList<MemoryCell> program = null, IList<MemoryCell> data = null)
    {
        var snapToGrid = configuration.SnapToGrid ?? false;
        var xOffset = configuration.X ?? 0;
        var yOffset = configuration.Y ?? 0;
        var width = configuration.Width ?? 16;
        var height = configuration.Height ?? 16;
        var programRows = configuration.ProgramRows ?? (program != null ? (program.Count - 1) / width + 1 : height / 2);
        var programName = configuration.ProgramName;
        var iconNames = configuration.IconNames ?? [ItemNames.ElectronicCircuit];

        if (program != null && program.Count > programRows * width)
        {
            Console.WriteLine($"Program too large to fit in ROM ({program.Count} > {programRows * width})");
            programRows = (program.Count - 1) / width + 1;
            height = Math.Max(height, programRows + ((data?.Count ?? 0) - 1) / width + 1);
        }

        if (data != null && data.Count > (height - programRows) * width)
        {
            Console.WriteLine($"Data too large to fit in ROM ({data.Count} > {(height - programRows) * width})");
            height = programRows + (data.Count - 1) / width + 1;
        }

        var cellHeight = 3;
        var blockHeightInCells = 64;
        var blockGapHeight = 8;

        var gridWidth = width + ((width + 7) / 16 + 1) * 2;
        var gridHeight = height * cellHeight + (height - 1) / blockHeightInCells * blockGapHeight;

        var entities = new List<Entity>();
        var wires = new List<Wire>();
        var memoryCellReaders = new Entity[height, width];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                var memoryCell = row < programRows
                    ? program?.ElementAtOrDefault(row * width + column) ?? new MemoryCell { Address = -1, IsEnabled = false }
                    : data?.ElementAtOrDefault((row - programRows) * width + column) ?? new MemoryCell { Address = -1, IsEnabled = false };
                var memoryCellX = column + (column / 16 + 1) * 2 + xOffset;
                var memoryCellY = gridHeight - (row + 1) * cellHeight - row / blockHeightInCells * blockGapHeight + yOffset;

                var adjacentMemoryCells = new List<Entity>();

                // Add left neighbor if it exists
                if (column > 0)
                {
                    adjacentMemoryCells.Add(memoryCellReaders[row, column - 1]);
                }

                // Add top neighbor if it exists, is in the same section (program/data), and is in the first column
                if (row > 0 && row != programRows && column == 0)
                {
                    adjacentMemoryCells.Add(memoryCellReaders[row - 1, column]);
                }

                var memoryCellData = new Entity
                {
                    Name = ItemNames.ConstantCombinator,
                    Position = new Position
                    {
                        X = memoryCellX,
                        Y = memoryCellY
                    },
                    Direction = Direction.Down,
                    Control_behavior = new ControlBehavior
                    {
                        Sections = Sections.Create(memoryCell.Filters),
                        Is_on = memoryCell.IsEnabled ? null : false
                    }
                };
                entities.Add(memoryCellData);

                var memoryCellReader = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = memoryCellX,
                        Y = memoryCellY + 1.5
                    },
                    Direction = Direction.Down,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            Conditions = [.. memoryCell.AddressRanges.SelectMany<(int Start, int End), DeciderCondition>(range =>
                            {
                                if (range.Start == range.End)
                                {
                                    return [new DeciderCondition
                                    {
                                        First_signal = SignalID.CreateVirtual(VirtualSignalNames.Info),
                                        Constant = range.Start,
                                        Comparator = Comparators.IsEqual,
                                        Compare_type = CompareTypes.Or
                                    }];
                                }
                                else
                                {
                                    return [
                                        new DeciderCondition
                                        {
                                            First_signal = SignalID.CreateVirtual(VirtualSignalNames.Info),
                                            Constant = range.Start,
                                            Comparator = Comparators.GreaterThanOrEqualTo,
                                            Compare_type = CompareTypes.Or
                                        },
                                        new DeciderCondition
                                        {
                                            First_signal = SignalID.CreateVirtual(VirtualSignalNames.Info),
                                            Constant = range.End,
                                            Comparator = Comparators.LessThanOrEqualTo,
                                            Compare_type = CompareTypes.And
                                        }
                                    ];
                                }
                            })],
                            Outputs = [new()
                            {
                                Signal = SignalID.CreateVirtual(VirtualSignalNames.Everything),
                                Copy_count_from_input = true,
                                Networks = new() { Green = true }
                            }]
                        }
                    }
                };
                entities.Add(memoryCellReader);
                memoryCellReaders[row, column] = memoryCellReader;

                // Connection to memory cell
                wires.Add(new((memoryCellReader, ConnectionType.Green1), (memoryCellData, ConnectionType.Green1)));

                foreach (var adjacentMemoryCell in adjacentMemoryCells)
                {
                    // Connection to adjacent reader input (address line)
                    wires.Add(new((memoryCellReader, ConnectionType.Red1), (adjacentMemoryCell, ConnectionType.Red1)));

                    // Connection to adjacent reader output
                    wires.Add(new((memoryCellReader, ConnectionType.Green2), (adjacentMemoryCell, ConnectionType.Green2)));
                }
            }
        }

        // Timestamp
        entities.Add(new Entity
        {
            Entity_number = entities.Count + 1,
            Name = ItemNames.ConstantCombinator,
            Position = new Position
            {
                X = 1 + xOffset,
                Y = gridHeight - 2 + yOffset
            },
            Direction = Direction.Down,
            Control_behavior = new ControlBehavior
            {
                Sections = Sections.Create([Filter.Create(VirtualSignalNames.LetterOrDigit('0'), (int)(DateTime.Now.Ticks / 10000))])
            }
        });

        var substationWidth = (width + 7) / 16 + 1;
        var substationHeight = (gridHeight + 2) / 18 + 1;

        PowerUtil.AddSubstations(entities, wires, substationWidth, substationHeight, xOffset, gridHeight % 18 - 4 + yOffset);

        BlueprintUtil.PopulateEntityNumbers(entities);

        return new Blueprint
        {
            Label = $"{width}x{height} ROM{(programName != null ? $": {programName}": "")}",
            Icons = [.. iconNames.Select(Icon.Create)],
            Entities = entities,
            Wires = [.. wires.Select(wire => wire.ToArray())],
            SnapToGrid = snapToGrid ? new() { X = (ulong)gridWidth, Y = (ulong)gridHeight } : null,
            AbsoluteSnapping = snapToGrid ? true : null,
            Item = ItemNames.Blueprint,
            Version = BlueprintVersions.CurrentVersion
        };
    }
}

public class RomConfiguration
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
    /// The number of rows to allocate for the program (the remainder go to data).
    /// </summary>
    public int? ProgramRows { get; set; }

    /// <summary>
    /// The name of the program.
    /// </summary>
    public string ProgramName { get; set; }

    /// <summary>
    /// The item or virtual signal names to use in the blueprint icon.
    /// </summary>
    public List<string> IconNames { get; set; }
}

public class MemoryCell
{
    public int Address { set => AddressRanges = [new(value, value)]; }
    public List<(int Start, int End)> AddressRanges { get; set; }
    public List<Filter> Filters { get; set; }
    public bool IsEnabled { get; set; } = true;
}
