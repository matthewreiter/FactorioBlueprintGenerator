using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using static MemoryInitializer.ConnectionUtil;
using Icon = BlueprintCommon.Models.Icon;

namespace MemoryInitializer
{
    public class FontGenerator
    {
        public static Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<FontConfiguration>());
        }

        public static Blueprint Generate(FontConfiguration configuration)
        {
            var fontImageFile = configuration.FontImage;
            var combinatorsPerRow = configuration.CombinatorsPerRow ?? 5;
            var inputSignal = configuration.InputSignal ?? VirtualSignalNames.Dot;
            var signals = configuration.Signals.Contains(',') ? configuration.Signals.Split(',').ToList() : configuration.Signals.Select(signal => VirtualSignalNames.LetterOrDigit(signal)).ToList();

            const int maxFilters = 20;

            var font = FontUtil.ReadFont(fontImageFile);
            var width = font.Width;
            var height = font.Height;
            var characters = font.Characters;

            var entities = new List<Entity>();
            var characterEntities = new List<(Entity Matcher, List<Entity> Glyph)>();

            var combinatorX = 0;

            for (var characterIndex = 0; characterIndex < characters.Count; characterIndex++)
            {
                var character = characters[characterIndex];
                var glyphPixels = character.GlyphPixels;

                if (characterIndex % combinatorsPerRow == 0)
                {
                    combinatorX = 0;
                }

                var glyphSignals = new List<SignalID>();

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (glyphPixels[y, x])
                        {
                            glyphSignals.Add(SignalID.Create(signals[y * width + x]));
                        }
                    }
                }

                var combinatorY = characterIndex / combinatorsPerRow;

                var matcher = new Entity
                {
                    Name = ItemNames.DeciderCombinator,
                    Position = new Position
                    {
                        X = (characterIndex % combinatorsPerRow - combinatorsPerRow) * 2 + 0.5,
                        Y = combinatorY
                    },
                    Direction = Direction.Left,
                    Control_behavior = new ControlBehavior
                    {
                        Decider_conditions = new DeciderConditions
                        {
                            First_signal = SignalID.Create(inputSignal),
                            Constant = character.CharacterCode,
                            Comparator = Comparators.IsEqual,
                            Output_signal = SignalID.Create(VirtualSignalNames.Everything),
                            Copy_count_from_input = true
                        }
                    }
                };
                entities.Add(matcher);

                var glyph = new List<Entity>();

                for (int index = 0; index < (glyphSignals.Count + maxFilters - 1) / maxFilters; index++)
                {
                    var glyphPart = new Entity
                    {
                        Name = ItemNames.ConstantCombinator,
                        Position = new Position
                        {
                            X = combinatorX++,
                            Y = combinatorY
                        },
                        Direction = Direction.Down,
                        Control_behavior = new ControlBehavior
                        {
                            Filters = glyphSignals.Skip(index * maxFilters).Take(maxFilters).Select(signal => new Filter { Signal = signal, Count = 1 }).ToList()
                        }
                    };
                    glyph.Add(glyphPart);
                    entities.Add(glyphPart);
                }

                characterEntities.Add((matcher, glyph));
            }

            BlueprintUtil.PopulateEntityNumbers(entities);

            for (int characterIndex = 0; characterIndex < characterEntities.Count; characterIndex++)
            {
                var (matcher, glyph) = characterEntities[characterIndex];

                AddConnection(CircuitColor.Green, matcher, CircuitId.Input, glyph[0], null); // Connect to constant combinators holding glyphs

                var adjacentCharacterIndex = characterIndex - (characterIndex / combinatorsPerRow == 0 ? 1 : combinatorsPerRow);
                if (adjacentCharacterIndex >= 0)
                {
                    var adjacentMatcher = characterEntities[adjacentCharacterIndex].Matcher;

                    AddConnection(CircuitColor.Red, matcher, CircuitId.Input, adjacentMatcher, CircuitId.Input); // Connect inputs together
                    AddConnection(CircuitColor.Green, matcher, CircuitId.Output, adjacentMatcher, CircuitId.Output); // Connect outputs together
                }

                // Connections between glyph parts
                for (int index = 1; index < glyph.Count; index++)
                {
                    AddConnection(CircuitColor.Green, glyph[index], null, glyph[index - 1], null);
                }
            }

            return new Blueprint
            {
                Label = $"{width}x{height} Font",
                Icons = new List<Icon>
                {
                    new Icon
                    {
                        Signal = SignalID.Create(ItemNames.ConstantCombinator)
                    },
                    new Icon
                    {
                        Signal = SignalID.CreateLetterOrDigit('A')
                    },
                    new Icon
                    {
                        Signal = SignalID.CreateLetterOrDigit('B')
                    },
                    new Icon
                    {
                        Signal = SignalID.CreateLetterOrDigit('C')
                    }
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };
        }
    }

    public class FontConfiguration
    {
        public string FontImage { get; init; }
        public int? CombinatorsPerRow { get; init; }
        public string InputSignal { get; init; }
        public string Signals { get; init; }
    }
}
