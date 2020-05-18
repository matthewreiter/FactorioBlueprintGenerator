using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using static MemoryInitializer.MemoryUtil;

namespace MemoryInitializer
{
    public static class RegisterGenerator
    {
        public static Blueprint Generate(IConfigurationRoot configuration)
        {
            var registerCount = int.TryParse(configuration["RegisterCount"], out var registerCountValue) ? registerCountValue : 16;

            const char memorySignal = 'A';
            const char writeSignal = '1';
            const char leftOperandSignal = '2';
            const char rightOperandSignal = '3';
            const char conditionSignal = '4';

            const int writerEntityOffset = -1;
            const int autoIncrementEntityOffset = -2;
            const int leftOperandEntityOffset = 1;
            const int rightOperandEntityOffset = 2;
            const int conditionEntityOffset = 3;

            const int entitiesPerRegister = 6;
            const int cellWidth = 8;
            var cellHeight = registerCount * 2;
            var xOffset = -cellWidth / 2;
            var yOffset = -cellHeight / 2;

            var entities = new List<Entity>();

            for (int row = 0; row < registerCount; row++)
            {
                var address = row + 1;
                var memoryCellEntityNumber = row * entitiesPerRegister + 3;
                var memoryCellX = 2.5 + xOffset;
                var memoryCellY = row * 2 + 1 + yOffset;

                List<int> GetAdjacentMemoryCells(params int[] offsets) => offsets
                    .Where(offset => row + offset >= 0 && row + offset < registerCount)
                    .Select(offset => memoryCellEntityNumber + offset * entitiesPerRegister)
                    .ToList();

                var previousMemoryCell = GetAdjacentMemoryCells(-1);
                var nextMemoryCell = GetAdjacentMemoryCells(1);
                var adjacentMemoryCells = GetAdjacentMemoryCells(-1, 1);

                // Memory cell
                entities.Add(new Entity
                {
                    Entity_number = memoryCellEntityNumber,
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = memoryCellX,
                        Y = memoryCellY
                    },
                    Direction = 2,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = new SignalID
                            {
                                Type = SignalTypes.Virtual,
                                Name = VirtualSignalNames.LetterOrDigit(writeSignal)
                            },
                            Constant = address,
                            Comparator = Comparators.IsNotEqual,
                            Output_signal = new SignalID
                            {
                                Type = SignalTypes.Virtual,
                                Name = VirtualSignalNames.LetterOrDigit(memorySignal)
                            },
                            Copy_count_from_input = true
                        }
                    },
                    Connections = CreateConnections(new ConnectionPoint
                    {
                        Red = new List<ConnectionData>
                        {
                            // Connection to own output (data feedback)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber,
                                Circuit_id = 2
                            },
                            // Connection to auto-increment output (data in)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber + autoIncrementEntityOffset,
                                Circuit_id = 2
                            }
                        },
                        // Connection to next writer input (address line)
                        Green = nextMemoryCell.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber + writerEntityOffset,
                            Circuit_id = 1
                        }).Concat(new List<ConnectionData>
                        {
                            // Connection to writer input (address line)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber + writerEntityOffset,
                                Circuit_id = 1
                            }
                        }).ToList()
                    }, new ConnectionPoint
                    {
                        Red = new List<ConnectionData>
                        {
                            // Connection to own input (data feedback)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber,
                                Circuit_id = 1
                            },
                            // Connection to writer output (data in)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber + writerEntityOffset,
                                Circuit_id = 2
                            },
                            // Connection to right operand input (data out)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber + rightOperandEntityOffset,
                                Circuit_id = 1
                            }
                        }
                    })
                });

                // Memory cell writer
                entities.Add(new Entity
                {
                    Entity_number = memoryCellEntityNumber + writerEntityOffset,
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = memoryCellX,
                        Y = memoryCellY - 1
                    },
                    Direction = 2,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = new SignalID
                            {
                                Type = SignalTypes.Virtual,
                                Name = VirtualSignalNames.LetterOrDigit(writeSignal)
                            },
                            Constant = address,
                            Comparator = Comparators.IsEqual,
                            Output_signal = new SignalID
                            {
                                Type = SignalTypes.Virtual,
                                Name = VirtualSignalNames.LetterOrDigit(memorySignal)
                            },
                            Copy_count_from_input = true
                        }
                    },
                    Connections = CreateConnections(new ConnectionPoint
                    {
                        // Connection to adjacent writer input (data in)
                        Red = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber + writerEntityOffset,
                            Circuit_id = 1
                        }).ToList(),
                        // Connection to previous memory cell input (address line)
                        Green = previousMemoryCell.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber,
                            Circuit_id = 1
                        }).Concat(new List<ConnectionData>
                        {
                            // Connection to memory cell input (address line)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber,
                                Circuit_id = 1
                            }
                        }).ToList(),
                    }, new ConnectionPoint
                    {
                        Red = new List<ConnectionData>
                        {
                            // Connection to memory cell output (data out)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber,
                                Circuit_id = 2
                            }
                        }
                    })
                });

                // Auto-increment writer
                entities.Add(new Entity
                {
                    Entity_number = memoryCellEntityNumber + autoIncrementEntityOffset,
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = memoryCellX - 2,
                        Y = memoryCellY - 1
                    },
                    Direction = 2,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = new SignalID
                            {
                                Type = SignalTypes.Virtual,
                                Name = VirtualSignalNames.LetterOrDigit(leftOperandSignal)
                            },
                            Constant = address,
                            Comparator = Comparators.IsEqual,
                            Output_signal = new SignalID
                            {
                                Type = SignalTypes.Virtual,
                                Name = VirtualSignalNames.LetterOrDigit(memorySignal)
                            },
                            Copy_count_from_input = true
                        }
                    },
                    Connections = CreateConnections(new ConnectionPoint
                    {
                        // Connection to adjacent auto-increment input (address line/data in)
                        Green = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber + autoIncrementEntityOffset,
                            Circuit_id = 1
                        }).ToList(),
                    }, new ConnectionPoint
                    {
                        Red = new List<ConnectionData>
                        {
                            // Connection to memory cell input (data out)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber,
                                Circuit_id = 1
                            }
                        }
                    })
                });

                // Left operand reader
                entities.Add(new Entity
                {
                    Entity_number = memoryCellEntityNumber + leftOperandEntityOffset,
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = memoryCellX + 2,
                        Y = memoryCellY - 1
                    },
                    Direction = 2,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = new SignalID
                            {
                                Type = SignalTypes.Virtual,
                                Name = VirtualSignalNames.LetterOrDigit(leftOperandSignal)
                            },
                            Constant = address,
                            Comparator = Comparators.IsEqual,
                            Output_signal = new SignalID
                            {
                                Type = SignalTypes.Virtual,
                                Name = VirtualSignalNames.LetterOrDigit(memorySignal)
                            },
                            Copy_count_from_input = true
                        }
                    },
                    Connections = CreateConnections(new ConnectionPoint
                    {
                        Red = new List<ConnectionData>
                        {
                            // Connection to right operand input (data out)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber + rightOperandEntityOffset,
                                Circuit_id = 1
                            },
                            // Connection to condition input (data out)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber + conditionEntityOffset,
                                Circuit_id = 1
                            }
                        },
                        // Connection to previous right operand input (address line)
                        Green = previousMemoryCell.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber + rightOperandEntityOffset,
                            Circuit_id = 1
                        }).Concat(new List<ConnectionData>
                        {
                            // Connection to right operand input (address line)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber + rightOperandEntityOffset,
                                Circuit_id = 1
                            },
                            // Connection to condition input (address line)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber + conditionEntityOffset,
                                Circuit_id = 1
                            }
                        }).ToList()
                    }, new ConnectionPoint
                    {
                        // Connection to adjacent left operand output (data out)
                        Red = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber + leftOperandEntityOffset,
                            Circuit_id = 2
                        }).ToList()
                    })
                });

                // Right operand reader
                entities.Add(new Entity
                {
                    Entity_number = memoryCellEntityNumber + rightOperandEntityOffset,
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = memoryCellX + 2,
                        Y = memoryCellY
                    },
                    Direction = 2,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = new SignalID
                            {
                                Type = SignalTypes.Virtual,
                                Name = VirtualSignalNames.LetterOrDigit(rightOperandSignal)
                            },
                            Constant = address,
                            Comparator = Comparators.IsEqual,
                            Output_signal = new SignalID
                            {
                                Type = SignalTypes.Virtual,
                                Name = VirtualSignalNames.LetterOrDigit(memorySignal)
                            },
                            Copy_count_from_input = true
                        }
                    },
                    Connections = CreateConnections(new ConnectionPoint
                    {
                        Red = new List<ConnectionData>
                        {
                            // Connection to memory cell output (data out)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber,
                                Circuit_id = 2
                            }
                        },
                        // Connection to next left operand input (address line)
                        Green = nextMemoryCell.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber + leftOperandEntityOffset,
                            Circuit_id = 1
                        }).Concat(new List<ConnectionData>
                        {
                            // Connection to left operand input (address line)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber + leftOperandEntityOffset,
                                Circuit_id = 1
                            }
                        }).ToList()
                    }, new ConnectionPoint
                    {
                        // Connection to adjacent right operand output (data out)
                        Red = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber + rightOperandEntityOffset,
                            Circuit_id = 2
                        }).ToList()
                    })
                });

                // Condition reader
                entities.Add(new Entity
                {
                    Entity_number = memoryCellEntityNumber + conditionEntityOffset,
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = memoryCellX + 4,
                        Y = memoryCellY - 1
                    },
                    Direction = 2,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = new SignalID
                            {
                                Type = SignalTypes.Virtual,
                                Name = VirtualSignalNames.LetterOrDigit(conditionSignal)
                            },
                            Constant = address,
                            Comparator = Comparators.IsEqual,
                            Output_signal = new SignalID
                            {
                                Type = SignalTypes.Virtual,
                                Name = VirtualSignalNames.LetterOrDigit(memorySignal)
                            },
                            Copy_count_from_input = true
                        }
                    },
                    Connections = CreateConnections(new ConnectionPoint
                    {
                        Red = new List<ConnectionData>
                        {
                            // Connection to left operand input (data out)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber + leftOperandEntityOffset,
                                Circuit_id = 1
                            }
                        },
                        Green = new List<ConnectionData>
                        {
                            // Connection to left operand input (address line)
                            new ConnectionData
                            {
                                Entity_id = memoryCellEntityNumber + leftOperandEntityOffset,
                                Circuit_id = 1
                            }
                        }
                    }, new ConnectionPoint
                    {
                        // Connection to adjacent condition output (data out)
                        Red = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                        {
                            Entity_id = entityNumber + conditionEntityOffset,
                            Circuit_id = 2
                        }).ToList()
                    })
                });
            }

            return new Blueprint
            {
                Label = $"{registerCount} Registers",
                Icons = new List<Icon>
                {
                    new Icon
                    {
                        Signal = new SignalID
                        {
                            Type = SignalTypes.Item,
                            Name = ItemNames.ProcessingUnit
                        }
                    }
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };
        }
    }
}
