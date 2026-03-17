using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace BlueprintGenerator;

public class TextDisplayGenerator : IBlueprintGenerator
{
    private static readonly List<(char Character, string SignalName)> CharacterTuples =
    [
        .. Enumerable.Range('0', 10).Concat(Enumerable.Range('A', 26)).Concat(Enumerable.Range('a', 26))
            .Select(letterOrDigit => ((char)letterOrDigit, VirtualSignalNames.LetterOrDigit(char.ToUpperInvariant((char)letterOrDigit)))),
        ('ü', VirtualSignalNames.LetterOrDigit('U')),
        (',', VirtualSignalNames.Comma),
        ('.', VirtualSignalNames.Period),
        ('!', VirtualSignalNames.ExclamationMark),
        ('?', VirtualSignalNames.QuestionMark),
        (':', VirtualSignalNames.Colon),
        ('/', VirtualSignalNames.Slash),
        ('\'', VirtualSignalNames.Apostrophe),
        ('"', VirtualSignalNames.QuotationMark),
        ('&', VirtualSignalNames.Ampersand),
        ('^', VirtualSignalNames.Caret),
        ('#', VirtualSignalNames.Pound),
        ('%', VirtualSignalNames.Percent),
        ('+', VirtualSignalNames.Plus),
        ('-', VirtualSignalNames.Minus),
        ('*', VirtualSignalNames.Multiplication),
        ('=', VirtualSignalNames.Equal),
        ('<', VirtualSignalNames.LessThan),
        ('>', VirtualSignalNames.GreaterThan),
        ('(', VirtualSignalNames.LeftParenthesis),
        (')', VirtualSignalNames.RightParenthesis),
        ('[', VirtualSignalNames.LeftSquareBracket),
        (']', VirtualSignalNames.RightSquareBracket),
        (' ', VirtualSignalNames.Black),
    ];
    private static readonly Dictionary<char, SignalID> CharacterMap = CharacterTuples.ToDictionary(tuple => tuple.Character, tuple => SignalID.Create(tuple.SignalName));

    public Blueprint Generate(IConfigurationRoot configuration)
    {
        return Generate(configuration.Get<TextDisplayConfiguration>());
    }

    public static Blueprint Generate(TextDisplayConfiguration configuration)
    {
        var startingSignal = configuration.StartingSignal ?? 'A';
        var length = configuration.Length;
        var format = configuration.Format;

        if (format is null)
        {
            format = new string('w', length ?? 0);
        }
        else if (format.Length > 0 && format.Length < length)
        {
            format = format.PadRight(length.Value, format[^1]);
        }
        else if (format.Length > length)
        {
            format = format[..length.Value];
        }

        var gridWidth = format.Length;
        var gridHeight = 3;
        var xOffset = -gridWidth / 2;
        var yOffset = -gridHeight / 2;

        var entities = new List<Entity>();
        var wires = new List<Wire>();
        List<Entity> characterDisplays = [];
        List<Entity> digitDisplays = [];
        var x = xOffset;
        var byteIndex = 0;
        var literalMode = false;
        var nextEscapeMode = false;

        Entity CreateDisplay(List<DisplayPanelParameter> parameters = null, SignalID icon = null)
        {
            var display = new Entity
            {
                Name = ItemNames.DisplayPanel,
                Position = new Position
                {
                    X = x++,
                    Y = yOffset
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Parameters = parameters
                },
                Icon = icon
            };
            entities.Add(display);

            return display;
        }

        foreach (var formatCharacter in format)
        {
            var escapeMode = nextEscapeMode;
            nextEscapeMode = false;

            switch (formatCharacter)
            {
                case 'w' when !literalMode && !escapeMode:
                    {
                        var characterSignal = SignalID.CreateLetterOrDigit((char)(startingSignal + byteIndex / 4));
                        var shiftAmount = byteIndex % 4 * 8;

                        var display = CreateDisplay(parameters: [.. CharacterMap.Select(pair => new DisplayPanelParameter
                        {
                            Condition = new()
                            {
                                First_signal = characterSignal,
                                Constant = pair.Key << shiftAmount,
                                Comparator = Comparators.IsEqual
                            },
                            Icon = pair.Value
                        })]);

                        if (characterDisplays.Count >= 4)
                        {
                            wires.Add(new((display, ConnectionType.Green1), (characterDisplays[^4], ConnectionType.Green1)));
                        }

                        characterDisplays.Add(display);

                        byteIndex++;
                    }

                    break;
                case 'd' when !literalMode && !escapeMode:
                    {
                        byteIndex = (byteIndex + 3) / 4 * 4; // Round up to the next multiple of 4 to ensure digit signals are aligned
                        var digitSignal = SignalID.CreateLetterOrDigit((char)(startingSignal + byteIndex / 4));

                        var display = CreateDisplay(parameters: [.. Enumerable.Range(0, 10).Select(value => new DisplayPanelParameter
                        {
                            Condition = new()
                            {
                                First_signal = digitSignal,
                                Constant = value,
                                Comparator = Comparators.IsEqual
                            },
                            Icon = SignalID.CreateLetterOrDigit((char)('0' + value))
                        })]);

                        if (digitDisplays.Count >= 1)
                        {
                            wires.Add(new((display, ConnectionType.Green1), (digitDisplays[^1], ConnectionType.Green1)));
                        }

                        digitDisplays.Add(display);

                        byteIndex += 4;
                    }

                    break;
                case '\'' when !escapeMode:
                    literalMode = !literalMode;
                    break;
                case '\\' when !escapeMode:
                    nextEscapeMode = true;
                    break;
                case '0' when escapeMode:
                    x++;
                    break;
                default:
                    CreateDisplay(icon: CharacterMap.GetValueOrDefault(formatCharacter));

                    break;
            }
        }

        BlueprintUtil.PopulateEntityNumbers(entities);

        return new Blueprint
        {
            Label = $"{length}x Text Display",
            Icons = [Icon.Create(ItemNames.DisplayPanel), Icon.Create(VirtualSignalNames.LetterOrDigit(startingSignal))],
            Entities = entities,
            Wires = wires.ToArrayList()
        };
    }
}

public class TextDisplayConfiguration
{
    public char? StartingSignal { get; set; }
    public int? Length { get; set; }
    public string Format { get; set; }
}
