using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using static BlueprintGenerator.ConnectionUtil;
using static BlueprintGenerator.PowerUtil;

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

        var entitiesPerCell = 2;
        var cellHeight = 3;
        var blockHeightInCells = 64;
        var blockGapHeight = 8;

        var readerEntityOffset = 1;

        var gridWidth = width + ((width + 7) / 16 + 1) * 2;
        var gridHeight = height * cellHeight + (height - 1) / blockHeightInCells * blockGapHeight;

        var entities = new List<Entity>();

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                var memoryCell = row < programRows
                    ? program?.ElementAtOrDefault(row * width + column) ?? new MemoryCell { Address = -1, IsEnabled = false }
                    : data?.ElementAtOrDefault((row - programRows) * width + column) ?? new MemoryCell { Address = -1, IsEnabled = false };
                var memoryCellEntityNumber = (row * width + column) * entitiesPerCell + 1;
                var memoryCellX = column + (column / 16 + 1) * 2 + xOffset;
                var memoryCellY = gridHeight - (row + 1) * cellHeight - row / blockHeightInCells * blockGapHeight + yOffset;

                var adjacentMemoryCells = new List<int> { -1, 1 }
                    .Where(offset => column + offset >= 0 && column + offset < width)
                    .Select(offset => memoryCellEntityNumber + offset * entitiesPerCell)
                    .Concat(new List<int> { -1, 1 }
                        .Where(offset => row + offset >= 0 && row + offset < height && (row < programRows == row + offset < programRows) && column == 0)
                        .Select(offset => memoryCellEntityNumber + offset * width * entitiesPerCell)
                    )
                    .ToList();

                // Memory cell
                entities.Add(new Entity
                {
                    Entity_number = memoryCellEntityNumber,
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
                    },
                    Connections = CreateConnections(new ConnectionPoint
                    {
                        Green =
                        [
                            // Connection to reader input or previous memory sub-cell
                            new()
                            {
                                Entity_id = memoryCellEntityNumber + readerEntityOffset,
                                Circuit_id = CircuitId.Input
                            }
                        ]
                    })
                });

                // Memory cell reader
                entities.Add(new Entity
                {
                    Entity_number = memoryCellEntityNumber + readerEntityOffset,
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
                            First_signal = SignalID.CreateVirtual(VirtualSignalNames.Info),
                            Constant = memoryCell.Address,
                            Comparator = Comparators.IsEqual,
                            Output_signal = SignalID.CreateVirtual(VirtualSignalNames.Everything),
                            Copy_count_from_input = true
                        }
                    },
                    Connections = CreateConnections(new ConnectionPoint
                    {
                        // Connection to adjacent reader input (address line)
                        Red = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber + readerEntityOffset,
                            Circuit_id = CircuitId.Input
                        }).ToList(),
                        Green =
                        [
                            // Connection to memory cell
                            new()
                            {
                                Entity_id = memoryCellEntityNumber
                            }
                        ]
                    }, new ConnectionPoint
                    {
                        // Connection to adjacent reader output
                        Green = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber + readerEntityOffset,
                            Circuit_id = CircuitId.Output
                        }).ToList()
                    })
                });
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

        entities.AddRange(CreateSubstations(substationWidth, substationHeight, xOffset, gridHeight % 18 - 4 + yOffset, entities.Count + 1));

        return new Blueprint
        {
            Label = $"{width}x{height} ROM{(programName != null ? $": {programName}": "")}",
            Icons = iconNames.Select(Icon.Create).ToList(),
            Entities = entities,
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
    public int Address { get; set; }
    public List<Filter> Filters { get; set; }
    public bool IsEnabled { get; set; } = true;
}
