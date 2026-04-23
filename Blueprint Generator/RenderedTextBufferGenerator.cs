using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace BlueprintGenerator;

public class RenderedTextBufferGenerator : IBlueprintGenerator
{
    public Blueprint Generate(IConfigurationRoot configuration)
    {
        return Generate(configuration.Get<RenderedTextBufferConfiguration>());
    }

    public static Blueprint Generate(RenderedTextBufferConfiguration configuration)
    {
        var width = configuration.Width ?? 16;
        var height = configuration.Height ?? 16;
        var baseAddress = configuration.BaseAddress ?? 0;
        var writeAddressSignal = SignalID.CreateVirtual(configuration.WriteAddressSignal ?? VirtualSignalNames.Check);
        var readAddressSignal = SignalID.CreateVirtual(configuration.ReadAddressSignal ?? VirtualSignalNames.Info);
        var readCheckerSignal = configuration.ReadCheckerSignal is not null ? SignalID.CreateVirtual(configuration.ReadCheckerSignal) : null;
        var dataSignal = SignalID.CreateVirtual(configuration.DataSignal ?? VirtualSignalNames.Everything);
        var clearSignal = SignalID.CreateVirtual(configuration.ClearSignal ?? VirtualSignalNames.Deny);
        var includePower = configuration.IncludePower ?? true;

        if (width % 2 != 0)
        {
            throw new ArgumentException("The width must be even.");
        }

        const int entitiesPerCell = 3;
        const int cellHeight = 6;

        const int writerEntityOffset = -1;
        const int readerEntityOffset = 1;

        var gridWidth = width + (includePower ? ((width + 7) / 16 + 1) * 2 : 0);
        var gridHeight = height * cellHeight;
        var xOffset = -gridWidth / 2;
        var yOffset = -gridHeight / 2;

        List<Entity> entities = [];
        List<Wire> wires = [];
        var dataHolders = new Entity[height, width];
        var writers = new Entity[height, width];
        var readers = new Entity[height, width];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                var address = row * width + column + baseAddress + 1;
                var memoryCellEntityNumber = (row * width + column) * entitiesPerCell + 2;
                var memoryCellX = column + (includePower ? (column / 16 + 1) * 2 : 0) + xOffset;
                var memoryCellY = gridHeight - (row + 1) * cellHeight + 2.5 + yOffset;

                List<Entity> adjacentDataHolders = [];
                List<Entity> adjacentWriters = [];
                List<Entity> adjacentReaders = [];

                // Add left neighbor if it exists
                var adjacentColumn = column - 1;
                if (adjacentColumn >= 0)
                {
                    adjacentDataHolders.Add(dataHolders[row, adjacentColumn]);
                    adjacentReaders.Add(readers[row, adjacentColumn]);
                }

                var skipColumn = column - 2;
                if (skipColumn >= 0)
                {
                    adjacentWriters.Add(writers[row, skipColumn]);
                }

                // Add top neighbor if it exists
                if (row > 0)
                {
                    adjacentDataHolders.Add(dataHolders[row - 1, column]);
                    adjacentWriters.Add(writers[row - 1, column]);
                    adjacentReaders.Add(readers[row - 1, column]);
                }

                var dataHolder = new Entity
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
                                    First_signal = clearSignal,
                                    First_signal_networks = new() { Red = true },
                                    Constant = 0,
                                    Comparator = Comparators.IsEqual,
                                    Compare_type = CompareTypes.And
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
                entities.Add(dataHolder);
                dataHolders[row, column] = dataHolder;

                // Connection to self (data feedback)
                wires.Add(new((dataHolder, ConnectionType.Green1), (dataHolder, ConnectionType.Green2)));

                foreach (var adjacentDataHolder in adjacentDataHolders)
                {
                    // Connection to adjacent data holder input (address line)
                    wires.Add(new((dataHolder, ConnectionType.Red1), (adjacentDataHolder, ConnectionType.Red1)));
                }

                var writer = new Entity
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
                entities.Add(writer);
                writers[row, column] = writer;

                // Connection to data holder input (data in)
                wires.Add(new((writer, ConnectionType.Green2), (dataHolder, ConnectionType.Green1)));

                foreach (var adjacentWriter in adjacentWriters)
                {
                    // Connection to adjacent writer input (address line)
                    wires.Add(new((writer, ConnectionType.Red1), (adjacentWriter, ConnectionType.Red1)));

                    // Connection to adjacent writer input (data in)
                    wires.Add(new((writer, ConnectionType.Green1), (adjacentWriter, ConnectionType.Green1)));
                }

                var reader = new Entity
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
                                    Second_signal = readCheckerSignal,
                                    Second_signal_networks = readCheckerSignal is not null ? new() { Green = true } : null,
                                    Constant = readCheckerSignal is null ? address : null,
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
                entities.Add(reader);
                readers[row, column] = reader;

                // Connection to memory cell output (data out)
                wires.Add(new((reader, ConnectionType.Green1), (dataHolder, ConnectionType.Green2)));

                foreach (var adjacentReader in adjacentReaders)
                {
                    // Connection to adjacent reader output (data out)
                    wires.Add(new((reader, ConnectionType.Green2), (adjacentReader, ConnectionType.Green2)));

                    // Connection to adjacent reader input (address line)
                    wires.Add(new((reader, ConnectionType.Red1), (adjacentReader, ConnectionType.Red1)));
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
            Label = $"{width}x{height} Rendered Text Buffer",
            Icons = [Icon.Create(ItemNames.AdvancedCircuit)],
            Entities = entities,
            Wires = wires.ToArrayList()
        };
    }
}

public class RenderedTextBufferConfiguration
{
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? BaseAddress { get; set; }
    public string WriteAddressSignal { get; set; }
    public string ReadAddressSignal { get; set; }
    public string ReadCheckerSignal { get; set; }
    public string ClearSignal { get; set; }
    public string DataSignal { get; set; }
    public bool? IncludePower { get; set; }
}
