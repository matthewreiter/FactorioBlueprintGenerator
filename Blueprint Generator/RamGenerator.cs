using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace BlueprintGenerator;

public class RamGenerator : IBlueprintGenerator
{
    public Blueprint Generate(IConfigurationRoot configuration)
    {
        return Generate(configuration.Get<RamConfiguration>());
    }

    public static Blueprint Generate(RamConfiguration configuration)
    {
        var width = configuration.Width ?? 16;
        var height = configuration.Height ?? 16;
        var baseAddress = configuration.BaseAddress ?? 0;
        var signalName = configuration.Signal ?? VirtualSignalNames.LetterOrDigit('0');
        var includeClearSignal = configuration.IncludeClearSignal ?? false;
        var includePower = configuration.IncludePower ?? true;

        const int entitiesPerCell = 3;
        const int cellHeight = 6;

        const int writerEntityOffset = -1;
        const int readerEntityOffset = 1;

        var gridWidth = width + (includePower ? ((width + 7) / 16 + 1) * 2 : 0);
        var gridHeight = height * cellHeight;
        var xOffset = -gridWidth / 2;
        var yOffset = -gridHeight / 2;

        var writeAddressSignal = SignalID.CreateVirtual(VirtualSignalNames.Check);
        var readAddressSignal = SignalID.CreateVirtual(VirtualSignalNames.Info);
        var dataSignal = SignalID.Create(signalName);
        var clearSignal = includeClearSignal ? SignalID.CreateVirtual(VirtualSignalNames.Deny) : null;

        List<Entity> entities = [];
        List<Wire> wires = [];
        var memoryCellWriters = new Entity[height, width];
        var memoryCellReaders = new Entity[height, width];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                var address = row * width + column + baseAddress + 1;
                var memoryCellEntityNumber = (row * width + column) * entitiesPerCell + 2;
                var memoryCellX = column + (includePower ? (column / 16 + 1) * 2 : 0) + xOffset;
                var memoryCellY = gridHeight - (row + 1) * cellHeight + 2.5 + yOffset;

                List<Entity> adjacentWriters = [];
                List<Entity> adjacentReaders = [];

                // Add left neighbor if it exists
                if (column > 0)
                {
                    adjacentWriters.Add(memoryCellWriters[row, column - 1]);
                    adjacentReaders.Add(memoryCellReaders[row, column - 1]);
                }

                // Add top neighbor if it exists
                if (row > 0)
                {
                    adjacentWriters.Add(memoryCellWriters[row - 1, column]);
                    adjacentReaders.Add(memoryCellReaders[row - 1, column]);
                }

                var memoryCellData = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = memoryCellX,
                        Y = memoryCellY
                    },
                    Direction = Direction.Down,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            Conditions =
                            [
                                new()
                                {
                                    First_signal = writeAddressSignal,
                                    First_signal_networks = new() { Red = true },
                                    Constant = address,
                                    Comparator = Comparators.IsNotEqual
                                },
                                .. includeClearSignal ? [
                                    new()
                                    {
                                        First_signal = clearSignal,
                                        First_signal_networks = new() { Red = true },
                                        Constant = 0,
                                        Comparator = Comparators.IsEqual,
                                        Compare_type = CompareTypes.And
                                    }
                                ] : Array.Empty<DeciderCondition>()
                            ],
                            Outputs =
                            [
                                new()
                                {
                                    Signal = dataSignal,
                                    Copy_count_from_input = true,
                                    Networks = new() { Green = true }
                                }
                            ]
                        }
                    }
                };
                entities.Add(memoryCellData);

                // Connection to self (data feedback)
                wires.Add(new((memoryCellData, ConnectionType.Green1), (memoryCellData, ConnectionType.Green2)));

                var memoryCellWriter = new Entity
                {
                    Entity_number = memoryCellEntityNumber + writerEntityOffset,
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = memoryCellX,
                        Y = memoryCellY - 2
                    },
                    Direction = Direction.Down,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            Conditions =
                            [
                                new()
                                {
                                    First_signal = writeAddressSignal,
                                    First_signal_networks = new() { Red = true },
                                    Constant = address,
                                    Comparator = Comparators.IsEqual
                                }
                            ],
                            Outputs =
                            [
                                new()
                                {
                                    Signal = dataSignal,
                                    Copy_count_from_input = true,
                                    Networks = new() { Green = true }
                                }
                            ]
                        }
                    }
                };
                entities.Add(memoryCellWriter);
                memoryCellWriters[row, column] = memoryCellWriter;

                // Connection to memory cell input (address line)
                wires.Add(new((memoryCellWriter, ConnectionType.Red1), (memoryCellData, ConnectionType.Red1)));

                // Connection to memory cell input (data in)
                wires.Add(new((memoryCellWriter, ConnectionType.Green2), (memoryCellData, ConnectionType.Green1)));

                foreach (var adjacentWriter in adjacentWriters)
                {
                    // Connection to adjacent writer input (address line)
                    wires.Add(new((memoryCellWriter, ConnectionType.Red1), (adjacentWriter, ConnectionType.Red1)));

                    // Connection to adjacent writer input (data in)
                    wires.Add(new((memoryCellWriter, ConnectionType.Green1), (adjacentWriter, ConnectionType.Green1)));
                }

                var memoryCellReader = new Entity
                {
                    Entity_number = memoryCellEntityNumber + readerEntityOffset,
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = memoryCellX,
                        Y = memoryCellY + 2
                    },
                    Direction = Direction.Down,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            Conditions =
                            [
                                new()
                                {
                                    First_signal = readAddressSignal,
                                    First_signal_networks = new() { Red = true },
                                    Constant = address,
                                    Comparator = Comparators.IsEqual
                                }
                            ],
                            Outputs =
                            [
                                new()
                                {
                                    Signal = dataSignal,
                                    Copy_count_from_input = true,
                                    Networks = new() { Green = true }
                                }
                            ]
                        }
                    }
                };
                entities.Add(memoryCellReader);
                memoryCellReaders[row, column] = memoryCellReader;

                // Connection to memory cell input (address line)
                wires.Add(new((memoryCellReader, ConnectionType.Red1), (memoryCellData, ConnectionType.Red1)));

                // Connection to memory cell output (data out)
                wires.Add(new((memoryCellReader, ConnectionType.Green1), (memoryCellData, ConnectionType.Green2)));

                foreach (var adjacentReader in adjacentReaders)
                {
                    // Connection to adjacent reader output (data out)
                    wires.Add(new((memoryCellReader, ConnectionType.Green2), (adjacentReader, ConnectionType.Green2)));
                }
            }
        }

        if (includePower)
        {
            var substationWidth = (width + 7) / 16 + 1;
            var substationHeight = (gridHeight + 3) / 18 + 1;

            PowerUtil.AddSubstations(entities, wires, substationWidth, substationHeight, xOffset, gridHeight % 18 - 4 + yOffset);
        }

        BlueprintUtil.PopulateEntityNumbers(entities);

        return new Blueprint
        {
            Label = $"{width}x{height} RAM",
            Icons = [Icon.Create(ItemNames.AdvancedCircuit)],
            Entities = entities,
            Wires = wires.ToArrayList()
        };
    }
}

public class RamConfiguration
{
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? BaseAddress { get; set; }
    public string Signal { get; set; }
    public bool? IncludeClearSignal {  get; set; }
    public bool? IncludePower { get; set; }
}
