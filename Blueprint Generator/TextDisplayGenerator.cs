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
    private static readonly List<(int Character, string SignalName)> CharacterMap =
    [
        .. Enumerable.Range('0', 10).Concat(Enumerable.Range('A', 26)).Concat(Enumerable.Range('a', 26))
            .Select(letterOrDigit => (letterOrDigit, VirtualSignalNames.LetterOrDigit(char.ToUpperInvariant((char)letterOrDigit)))),
        ('ü', VirtualSignalNames.LetterOrDigit('U')),
        (',', "signal-comma"),
        ('.', "signal-letter-dot"),
        ('!', "signal-exclamation-mark"),
        ('?', "signal-question-mark"),
        (':', "signal-colon"),
        ('/', "signal-slash"),
        ('\'', "signal-apostrophe"),
        ('"', "signal-quotation-mark"),
        ('&', "signal-ampersand"),
        ('^', "signal-circumflex-accent"),
        ('#', "signal-number-sign"),
        ('%', "signal-percent"),
        ('+', "signal-plus"),
        ('-', "signal-minus"),
        ('*', "signal-multiplication"),
        ('=', "signal-equal"),
        ('<', "signal-less-than"),
        ('>', "signal-greater-than"),
        ('(', "signal-left-parenthesis"),
        (')', "signal-right-parenthesis"),
        ('[', "signal-left-square-bracket"),
        (']', "signal-right-square-bracket"),
    ];

    public Blueprint Generate(IConfigurationRoot configuration)
    {
        return Generate(configuration.Get<TextDisplayConfiguration>());
    }

    public static Blueprint Generate(TextDisplayConfiguration configuration)
    {
        var startingSignal = configuration.StartingSignal ?? 'A';
        var length = configuration.Length ?? 1;

        var gridWidth = length;
        var gridHeight = 3;
        var xOffset = -gridWidth / 2;
        var yOffset = -gridHeight / 2;

        var intermediateSignal = SignalID.CreateVirtual(VirtualSignalNames.Dot);

        var entities = new List<Entity>();
        var wires = new List<Wire>();
        Entity previousDecoder = null;

        for (int characterIndex = 0; characterIndex < length; characterIndex++)
        {
            var characterSignal = SignalID.CreateLetterOrDigit((char)(startingSignal + characterIndex / 4));
            var shiftAmount = characterIndex % 4 * 8;

            var characterDisplay = new Entity
            {
                Name = ItemNames.DisplayPanel,
                Position = new Position
                {
                    X = xOffset + characterIndex,
                    Y = yOffset
                },
                Direction = Direction.Down,
                Control_behavior = new ControlBehavior
                {
                    Parameters = [.. CharacterMap.Select(tuple => new DisplayPanelParameter
                    {
                        Condition = new()
                        {
                            First_signal = intermediateSignal,
                            Constant = tuple.Character << shiftAmount,
                            Comparator = Comparators.IsEqual
                        },
                        Icon = SignalID.Create(tuple.SignalName)
                    })]
                }
            };
            entities.Add(characterDisplay);

            var decoder = new Entity
            {
                Name = ItemNames.ArithmeticCombinator,
                Position = new Position
                {
                    X = xOffset + characterIndex,
                    Y = yOffset + 1.5
                },
                Direction = Direction.Up,
                Control_behavior = new ControlBehavior
                {
                    Arithmetic_conditions = new()
                    {
                        First_signal = characterSignal,
                        Operation = ArithmeticOperations.And,
                        Second_constant = 0xFF << shiftAmount,
                        Output_signal = intermediateSignal
                    }
                }
            };
            entities.Add(decoder);

            wires.Add(new((decoder, ConnectionType.Green2), (characterDisplay, ConnectionType.Green1)));

            if (characterIndex > 0)
            {
                wires.Add(new((decoder, ConnectionType.Green1), (previousDecoder, ConnectionType.Green1)));
            }

            previousDecoder = decoder;
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
}
