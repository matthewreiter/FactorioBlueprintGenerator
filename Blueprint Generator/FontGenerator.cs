using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Icon = BlueprintCommon.Models.Icon;

namespace BlueprintGenerator;

[SupportedOSPlatform("windows")]
public class FontGenerator : IBlueprintGenerator
{
    public Blueprint Generate(IConfigurationRoot configuration)
    {
        return Generate(configuration.Get<FontConfiguration>());
    }

    public static Blueprint Generate(FontConfiguration configuration)
    {
        var fontImageFile = configuration.FontImage;
        var combinatorsPerRow = configuration.CombinatorsPerRow ?? 5;
        var useOneSignalPerRow = configuration.UseOneSignalPerRow ?? false;
        var inputSignal = configuration.InputSignal ?? VirtualSignalNames.Dot;
        var widthSignal = configuration.WidthSignal;
        var heightSignal = configuration.HeightSignal;
        var signals = configuration.Signals.Contains(',') ? [.. configuration.Signals.Split(',')] : configuration.Signals.Select(VirtualSignalNames.LetterOrDigit).ToList();

        var font = FontUtil.ReadFont(fontImageFile);
        var characters = font.Characters;

        List<Entity> entities = [];
        List<Wire> wires = [];
        List<Entity> matchers = [];

        if (heightSignal is not null)
        {
            characters.Add(new FontUtil.Character
            {
                CharacterCode = '\n',
                GlyphPixels = new bool[font.Height, 0]
            });
        }

        for (var characterIndex = 0; characterIndex < characters.Count; characterIndex++)
        {
            var character = characters[characterIndex];
            var glyphPixels = character.GlyphPixels;
            var height = glyphPixels.GetLength(0);
            var width = glyphPixels.GetLength(1);

            var glyphFilters = new List<Filter>();

            if (widthSignal != null)
            {
                glyphFilters.Add(Filter.Create(widthSignal, width));
            }

            if (heightSignal != null)
            {
                glyphFilters.Add(Filter.Create(heightSignal, height));
            }

            for (int y = 0; y < height; y++)
            {
                if (useOneSignalPerRow)
                {
                    var rowSignal = 0;

                    for (int x = 0; x < width; x++)
                    {
                        if (glyphPixels[y, x])
                        {
                            rowSignal |= 1 << x;
                        }
                    }

                    glyphFilters.Add(Filter.Create(signals[y], rowSignal));
                }
                else
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (glyphPixels[y, x])
                        {
                            glyphFilters.Add(Filter.Create(signals[y * width + x]));
                        }
                    }
                }
            }

            var combinatorX = (characterIndex % combinatorsPerRow - combinatorsPerRow) * 3;
            var combinatorY = characterIndex / combinatorsPerRow;

            var matcher = new Entity
            {
                Name = ItemNames.DeciderCombinator,
                Position = new Position
                {
                    X = combinatorX + 0.5,
                    Y = combinatorY
                },
                Direction = Direction.Left,
                Control_behavior = new ControlBehavior
                {
                    Decider_conditions = new DeciderConditions
                    {
                        Conditions =
                        [
                            new()
                            {
                                First_signal = SignalID.Create(inputSignal),
                                First_signal_networks = new() { Red = true },
                                Constant = character.CharacterCode,
                                Comparator = Comparators.IsEqual
                            }
                        ],
                        Outputs =
                        [
                            new()
                            {
                                Signal = SignalID.Create(VirtualSignalNames.Everything),
                                Copy_count_from_input = true,
                                Networks = new() { Green = true }
                            }
                        ]
                    }
                }
            };
            entities.Add(matcher);

            var glyph = new Entity
            {
                Name = ItemNames.ConstantCombinator,
                Position = new Position
                {
                    X = combinatorX + 2,
                    Y = combinatorY
                },
                Direction = Direction.Left,
                Control_behavior = new ControlBehavior
                {
                    Sections = Sections.Create(glyphFilters)
                }
            };
            entities.Add(glyph);
            matchers.Add(matcher);

            wires.Add(new((matcher, ConnectionType.Green1), (glyph, ConnectionType.Green1))); // Connect to constant combinators holding glyphs

            var adjacentCharacterIndex = characterIndex - (characterIndex / combinatorsPerRow == 0 ? 1 : combinatorsPerRow);
            if (adjacentCharacterIndex >= 0)
            {
                var adjacentMatcher = matchers[adjacentCharacterIndex];

                wires.Add(new((matcher, ConnectionType.Red1), (adjacentMatcher, ConnectionType.Red1))); // Connect inputs together
                wires.Add(new((matcher, ConnectionType.Green2), (adjacentMatcher, ConnectionType.Green2))); // Connect outputs together
            }
        }

        BlueprintUtil.PopulateEntityNumbers(entities);

        return new Blueprint
        {
            Label = $"{font.Width}x{font.Height} Font",
            Icons =
            [
                Icon.Create(ItemNames.ConstantCombinator),
                Icon.Create(VirtualSignalNames.LetterOrDigit('A')),
                Icon.Create(VirtualSignalNames.LetterOrDigit('B')),
                Icon.Create(VirtualSignalNames.LetterOrDigit('C'))
            ],
            Entities = entities,
            Wires = wires.ToArrayList()
        };
    }
}

public class FontConfiguration
{
    public string FontImage { get; init; }
    public int? CombinatorsPerRow { get; init; }
    public bool? UseOneSignalPerRow { get; init; }
    public string InputSignal { get; init; }
    public string WidthSignal { get; init; }
    public string HeightSignal { get; init; }
    public string Signals { get; init; }
}
