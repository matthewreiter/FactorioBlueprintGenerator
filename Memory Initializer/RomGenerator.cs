﻿using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using static MemoryInitializer.MemoryUtil;

namespace MemoryInitializer
{
    public static class RomGenerator
    {
        public static Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<RomConfiguration>());
        }

        public static Blueprint Generate(RomConfiguration configuration, IList<MemoryCell> program = null, IList<MemoryCell> data = null)
        {
            var width = configuration.Width ?? 16;
            var height = configuration.Height ?? 16;
            var programRows = configuration.ProgramRows ?? (program != null ? (program.Count - 1) / width + 1 : height / 2);
            var programName = configuration.ProgramName;

            if (program != null && program.Count > programRows * width)
            {
                throw new Exception($"Program too large to fit in ROM ({program.Count} > {programRows * width})");
            }

            if (data != null && data.Count > (height - programRows) * width)
            {
                throw new Exception($"Data too large to fit in ROM ({data.Count} > {(height - programRows) * width})");
            }

            var cellWidth = width + ((width + 7) / 16 + 1) * 2;
            var cellHeight = height * 3;
            var xOffset = -cellWidth / 2;
            var yOffset = -cellHeight / 2;

            var entities = new List<Entity>();

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    var memoryCell = row < programRows
                        ? program?.ElementAtOrDefault(row * width + column) ?? new MemoryCell { Address = row * width + column + 1 + GetAddressOffset(program) }
                        : data?.ElementAtOrDefault((row - programRows) * width + column) ?? new MemoryCell { Address = (row - programRows) * width + column + 1 + GetAddressOffset(data) };
                    var memoryCellEntityNumber = (row * width + column) * 2 + 1;
                    var memoryCellX = column + (column / 16 + 1) * 2 + xOffset;
                    var memoryCellY = (height - row - 1) * 3 + yOffset;

                    var adjacentMemoryCells = new List<int> { -1, 1 }
                        .Where(offset => column + offset >= 0 && column + offset < width)
                        .Select(offset => memoryCellEntityNumber + offset * 2)
                        .Concat(new List<int> { -1, 1 }
                            .Where(offset => row + offset >= 0 && row + offset < height && (row < programRows == row + offset < programRows) && column == 0)
                            .Select(offset => memoryCellEntityNumber + offset * width * 2)
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
                        Direction = 4,
                        Control_behavior = new ControlBehavior
                        {
                            Filters = memoryCell.Filters
                        },
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            Green = new List<ConnectionData>
                            {
                                // Connection to reader input
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber + 1,
                                    Circuit_id = 1
                                }
                            }
                        })
                    });

                    // Memory cell reader
                    entities.Add(new Entity
                    {
                        Entity_number = memoryCellEntityNumber + 1,
                        Name = ItemNames.DeciderCombinator,
                        Position = new Position
                        {
                            X = memoryCellX,
                            Y = memoryCellY + 1.5
                        },
                        Direction = 4,
                        Control_behavior = new ControlBehavior
                        {
                            Decider_conditions = new DeciderConditions
                            {
                                First_signal = new SignalID
                                {
                                    Type = SignalTypes.Virtual,
                                    Name = VirtualSignalNames.Info
                                },
                                Constant = memoryCell.Address,
                                Comparator = Comparators.IsEqual,
                                Output_signal = new SignalID
                                {
                                    Type = SignalTypes.Virtual,
                                    Name = VirtualSignalNames.Everything
                                },
                                Copy_count_from_input = true
                            }
                        },
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            // Connection to adjacent reader input (address line)
                            Red = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber + 1,
                                Circuit_id = 1
                            }).ToList(),
                            Green = new List<ConnectionData>
                            {
                                // Connection to memory cell
                                new ConnectionData
                                {
                                    Entity_id = memoryCellEntityNumber
                                }
                            }
                        }, new ConnectionPoint
                        {
                            // Connection to adjacent reader output
                            Green = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber + 1,
                                Circuit_id = 2
                            }).ToList()
                        })
                    });
                }
            }

            var substationWidth = (width + 7) / 16 + 1;
            var substationHeight = (cellHeight + 3) / 18 + 1;

            entities.AddRange(CreateSubstations(substationWidth, substationHeight, xOffset, yOffset, width * height * 2 + 1));

            return new Blueprint
            {
                Label = $"{width}x{height} ROM{(programName != null ? $": {programName}": "")}",
                Icons = new List<Icon>
                {
                    new Icon
                    {
                        Signal = new SignalID
                        {
                            Type = SignalTypes.Item,
                            Name = ItemNames.ElectronicCircuit
                        }
                    }
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };
        }

        private static int GetAddressOffset(IList<MemoryCell> memoryCells) =>
            memoryCells?.Count > 0 ? memoryCells.Last().Address - memoryCells.Count : 0;
    }

    public class RomConfiguration
    {
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? ProgramRows { get; set; }
        public string ProgramName { get; set; }
    }

    public class MemoryCell
    {
        public int Address { get; set; }
        public List<Filter> Filters { get; set; }
    }
}