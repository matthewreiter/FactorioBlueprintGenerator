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

            const int entitiesPerController = 3;
            const int writerEntityOffset = 1;
            const int addressMatcherEntityOffset = 2;

            var entities = new List<Entity>();

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

                    var entityNumber = row * width + column + 1;

                    var adjacentLamps = new List<int> { -1, 1 }
                        .Select(offset => (row + offset + 18) % 18 > 15 ? offset * 3 : offset)
                        .Where(offset => row + offset >= 0 && row + offset < height)
                        .Select(offset => entityNumber + offset * width)
                        .ToList();

                    // Pixel
                    entities.Add(new Entity
                    {
                        Entity_number = entityNumber,
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
                        },
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            // Connections to adjacent lamps
                            Green = adjacentLamps.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber
                            }).Concat(row == height - 1
                                ? new List<ConnectionData>
                                {
                                    // Connection to memory output (data in)
                                    new ConnectionData
                                    {
                                        Entity_id = width * height + column * entitiesPerController + 1,
                                        Circuit_id = CircuitId.Output
                                    }
                                }
                                : new List<ConnectionData> { })
                            .ToList()
                        })
                    });
                }
            }

            // Column controllers
            for (var column = 0; column < width; column++)
            {
                var controllerEntityNumber = width * height + column * entitiesPerController + 1;

                var adjacentControllers = new List<int> { -1, 1 }
                    .Where(offset => column + offset >= 0 && column + offset < width)
                    .Select(offset => controllerEntityNumber + offset * entitiesPerController)
                    .ToList();

                // Memory
                entities.Add(new Entity
                {
                    Entity_number = controllerEntityNumber,
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
                    },
                    Connections = CreateConnections(new ConnectionPoint
                    {
                        // Connection to adjacent memory input (color in)
                        Red = adjacentControllers.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber,
                            Circuit_id = CircuitId.Input
                        }).ToList(),
                        Green = new List<ConnectionData>
                        {
                            // Connection to writer output (data in)
                            new ConnectionData
                            {
                                Entity_id = controllerEntityNumber + writerEntityOffset,
                                Circuit_id = CircuitId.Output
                            },
                            // Connection to own output (data feedback)
                            new ConnectionData
                            {
                                Entity_id = controllerEntityNumber,
                                Circuit_id = CircuitId.Output
                            }
                        }
                    }, new ConnectionPoint
                    {
                        Green = new List<ConnectionData>
                        {
                            // Connection to pixels (data out)
                            new ConnectionData
                            {
                                Entity_id = (height - 1) * width + column + 1
                            },
                            // Connection to own input (data feedback)
                            new ConnectionData
                            {
                                Entity_id = controllerEntityNumber,
                                Circuit_id = CircuitId.Input
                            }
                        }
                    })
                });

                // Writer
                entities.Add(new Entity
                {
                    Entity_number = controllerEntityNumber + writerEntityOffset,
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
                    },
                    Connections = CreateConnections(new ConnectionPoint
                    {
                        Red = new List<ConnectionData>
                        {
                            // Connection to address matcher output (enable in)
                            new ConnectionData
                            {
                                Entity_id = controllerEntityNumber + addressMatcherEntityOffset,
                                Circuit_id = CircuitId.Output
                            }
                        },
                        // Connection to adjacent writer input (data in)
                        Green = adjacentControllers.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber + writerEntityOffset,
                            Circuit_id = CircuitId.Input
                        }).ToList()
                    }, new ConnectionPoint
                    {
                        Green = new List<ConnectionData>
                        {
                            // Connection to memory input (data out)
                            new ConnectionData
                            {
                                Entity_id = controllerEntityNumber,
                                Circuit_id = CircuitId.Input
                            }
                        }
                    })
                });

                // Address matcher
                entities.Add(new Entity
                {
                    Entity_number = controllerEntityNumber + addressMatcherEntityOffset,
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
                    },
                    Connections = CreateConnections(new ConnectionPoint
                    {
                        // Connection to adjacent address matcher (address in)
                        Red = adjacentControllers.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber + addressMatcherEntityOffset,
                            Circuit_id = CircuitId.Input
                        }).ToList()
                    }, new ConnectionPoint
                    {
                        Red = new List<ConnectionData>
                        {
                            // Connection to memory writer input (enable out)
                            new ConnectionData
                            {
                                Entity_id = controllerEntityNumber + writerEntityOffset,
                                Circuit_id = CircuitId.Input
                            }
                        }
                    })
                });
            }

            var substationWidth = (width + 8) / 18 + 1;
            var substationHeight = (height + 8) / 18 + 1;

            entities.AddRange(CreateSubstations(substationWidth, substationHeight, 0, 0, width * (height + entitiesPerController) + 1, GridConnectivity.Top | GridConnectivity.Vertical));

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
