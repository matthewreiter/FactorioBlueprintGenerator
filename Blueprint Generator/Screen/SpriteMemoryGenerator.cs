using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Constants;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using static BlueprintGenerator.ConnectionUtil;

namespace BlueprintGenerator.Screen
{
    public class SpriteMemoryGenerator : IBlueprintGenerator
    {
        public Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<SpriteMemoryConfiguration>());
        }

        public static Blueprint Generate(SpriteMemoryConfiguration configuration)
        {
            var spriteCount = configuration.SpriteCount ?? 16;
            var baseAddress = configuration.BaseAddress ?? 1;

            var rowSignals = ComputerSignals.OrderedSignals.Take(36).ToList();
            var inputSignal = VirtualSignalNames.LetterOrDigit('0');

            var entities = new List<Entity>();
            var sprites = new Sprite[spriteCount];
            var rowFilters = new RowFilter[rowSignals.Count + 1];

            for (var index = 0; index < spriteCount; index++)
            {
                var reader = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = 0.5,
                        Y = index
                    },
                    Direction = Direction.Left,
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
                entities.Add(reader);

                var drawSelector = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = 2.5,
                        Y = index
                    },
                    Direction = Direction.Left,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Dot),
                            Constant = index + 1,
                            Comparator = Comparators.IsEqual,
                            Output_signal = SignalID.Create(VirtualSignalNames.Everything),
                            Copy_count_from_input = true
                        }
                    }
                };
                entities.Add(drawSelector);

                var memory = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = 4.5,
                        Y = index
                    },
                    Direction = Direction.Left,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Info),
                            Constant = 0,
                            Comparator = Comparators.IsEqual,
                            Output_signal = SignalID.Create(VirtualSignalNames.Everything),
                            Copy_count_from_input = true
                        }
                    }
                };
                entities.Add(memory);

                var writer = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = 6.5,
                        Y = index
                    },
                    Direction = Direction.Left,
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

                var spriteSelector = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = 8.5,
                        Y = index
                    },
                    Direction = Direction.Left,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Info),
                            Constant = index + 1,
                            Comparator = Comparators.IsNotEqual,
                            Output_signal = SignalID.Create(VirtualSignalNames.Check),
                            Copy_count_from_input = false
                        }
                    }
                };
                entities.Add(spriteSelector);

                sprites[index] = new Sprite
                {
                    Reader = reader,
                    DrawSelector = drawSelector,
                    Memory = memory,
                    Writer = writer,
                    SpriteSelector = spriteSelector
                };
            }

            for (var index = 0; index < rowFilters.Length; index++)
            {
                var renamer = new Entity
                {
                    Name = index == 0 ? ItemNames.DeciderCombinator : ItemNames.ArithmeticCombinator,
                    Position = new Position
                    {
                        X = 10.5,
                        Y = index
                    },
                    Direction = Direction.Left,
                    Control_behavior = index == 0
                        ? new ControlBehavior
                        {
                            Decider_conditions = new DeciderConditions
                            {
                                First_signal = SignalID.Create(inputSignal),
                                Constant = 1,
                                Comparator = Comparators.IsEqual,
                                Output_signal = SignalID.Create(VirtualSignalNames.Info),
                                Copy_count_from_input = false
                            }
                        }
                        : new ControlBehavior
                        {
                            Arithmetic_conditions = new ArithmeticConditions
                            {
                                First_signal = SignalID.Create(inputSignal),
                                Second_constant = 1,
                                Operation = ArithmeticOperations.Multiplication,
                                Output_signal = SignalID.Create(rowSignals[index - 1])
                            }
                        }
                };
                entities.Add(renamer);

                var addressMatcher = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = 12.5,
                        Y = index
                    },
                    Direction = Direction.Left,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(VirtualSignalNames.Check),
                            Constant = baseAddress + index,
                            Comparator = Comparators.IsEqual,
                            Output_signal = SignalID.Create(inputSignal),
                            Copy_count_from_input = true
                        }
                    }
                };
                entities.Add(addressMatcher);

                rowFilters[index] = new RowFilter
                {
                    Renamer = renamer,
                    AddressMatcher = addressMatcher
                };
            }

            BlueprintUtil.PopulateEntityNumbers(entities);

            AddConnection(CircuitColor.Green, sprites[0].Writer, CircuitId.Input, rowFilters[0].Renamer, CircuitId.Output);

            for (var index = 0; index < spriteCount; index++)
            {
                var sprite = sprites[index];

                AddConnection(CircuitColor.Green, sprite.Reader, CircuitId.Input, sprite.DrawSelector, CircuitId.Input);
                AddConnection(CircuitColor.Green, sprite.DrawSelector, CircuitId.Input, sprite.Memory, CircuitId.Output);
                AddConnection(CircuitColor.Green, sprite.Memory, CircuitId.Output, sprite.Memory, CircuitId.Input);
                AddConnection(CircuitColor.Red, sprite.Memory, CircuitId.Input, sprite.Writer, CircuitId.Output);
                AddConnection(CircuitColor.Red, sprite.Reader, CircuitId.Input, sprite.Writer, CircuitId.Input);
                AddConnection(CircuitColor.Red, sprite.Writer, CircuitId.Input, sprite.SpriteSelector, CircuitId.Output);

                if (index > 0)
                {
                    var adjacentSprite = sprites[index - 1];

                    AddConnection(CircuitColor.Green, sprite.Reader, CircuitId.Output, adjacentSprite.Reader, CircuitId.Output);
                    AddConnection(CircuitColor.Green, sprite.DrawSelector, CircuitId.Output, adjacentSprite.DrawSelector, CircuitId.Output);
                    AddConnection(CircuitColor.Red, sprite.DrawSelector, CircuitId.Input, adjacentSprite.DrawSelector, CircuitId.Input);
                    AddConnection(CircuitColor.Green, sprite.Writer, CircuitId.Input, adjacentSprite.Writer, CircuitId.Input);
                    AddConnection(CircuitColor.Green, sprite.SpriteSelector, CircuitId.Input, adjacentSprite.SpriteSelector, CircuitId.Input);
                }
            }

            for (var index = 0; index < rowFilters.Length; index++)
            {
                var rowFilter = rowFilters[index];

                AddConnection(CircuitColor.Green, rowFilter.Renamer, CircuitId.Input, rowFilter.AddressMatcher, CircuitId.Output);

                if (index > 0)
                {
                    var adjacentRowFilter = rowFilters[index - 1];

                    AddConnection(CircuitColor.Green, rowFilter.Renamer, CircuitId.Output, adjacentRowFilter.Renamer, CircuitId.Output);
                    AddConnection(CircuitColor.Green, rowFilter.AddressMatcher, CircuitId.Input, adjacentRowFilter.AddressMatcher, CircuitId.Input);
                    AddConnection(CircuitColor.Red, rowFilter.AddressMatcher, CircuitId.Input, adjacentRowFilter.AddressMatcher, CircuitId.Input);
                }
            }

            return new Blueprint
            {
                Label = $"Sprite Memory",
                Icons = new List<Icon>
                {
                    Icon.Create(ItemNames.DeciderCombinator),
                    Icon.Create(ItemNames.Lamp)
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };
        }

        private class Sprite
        {
            public Entity Reader { get; set; }
            public Entity DrawSelector { get; set; }
            public Entity Memory { get; set; }
            public Entity Writer { get; set; }
            public Entity SpriteSelector { get; set; }

        }

        private class RowFilter
        {
            public Entity Renamer { get; set; }
            public Entity AddressMatcher { get; set; }
        }
    }

    public class SpriteMemoryConfiguration
    {
        public int? SpriteCount { get; init; }
        public int? BaseAddress { get; init; }
    }
}
