using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicBoxCompiler.Utils;

public class FilterUtils
{
    public static IEnumerable<Filter> CreateFiltersForString(string text, int maxCharactersToDisplay, char initialSignal)
    {
        List<string> signalNames = [.. Enumerable.Range(0, maxCharactersToDisplay / 4).Select(index => VirtualSignalNames.LetterOrDigit((char)(initialSignal + index)))];

        return CreateFiltersForString(text, maxCharactersToDisplay, signalNames);
    }

    public static IEnumerable<Filter> CreateFiltersForString(string text, int maxCharactersToDisplay, List<string> signalNames)
    {
        var charactersToDisplay = Math.Min(text.Length, maxCharactersToDisplay);
        var encodedBlock = 0;
        var blockIndex = 0;

        for (var index = 0; index < charactersToDisplay; index++)
        {
            var currentCharacter = text[index];
            var positionInBlock = index % 4;

            var normalizedCharacter = currentCharacter switch
            {
                '‘' or '’' or '\u0092' => '\'',
                '“' or '”' => '"',
                _ => currentCharacter
            };

            encodedBlock |= (byte)normalizedCharacter << (positionInBlock * 8);

            if (positionInBlock == 3 || index == charactersToDisplay - 1)
            {
                yield return Filter.Create(signalNames[blockIndex], encodedBlock);
                encodedBlock = 0;
                blockIndex++;
            }
        }
    }
}
