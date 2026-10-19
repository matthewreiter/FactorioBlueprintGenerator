using BlueprintGenerator.Constants;
using ExcelDataReader;
using MusicBoxCompiler.Models;
using MusicBoxCompiler.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MusicBoxCompiler.SongReaders;

public static partial class SpreadsheetReader
{
    [GeneratedRegex(@"^((?:\d|[.])+)([#b]?)([BR]?)$")]
    private static partial Regex CreateSpreadsheetNoteRegex();
    private static readonly Regex SpreadsheetNoteRegex = CreateSpreadsheetNoteRegex();

    [GeneratedRegex(@"^(.+?): (\d+)-(\d+)$")]
    private static partial Regex CreateInstrumentMappingRegex();
    private static readonly Regex InstrumentMappingRegex = CreateInstrumentMappingRegex();

    [GeneratedRegex(@"^(.+?): (-?\d+)$")]
    private static partial Regex CreateInstrumentOffsetRegex();
    private static readonly Regex InstrumentOffsetRegex = CreateInstrumentOffsetRegex();

    [GeneratedRegex(@"^(?:(.+?): )?(-?\d+(?:\.\d+)?)$")]
    private static partial Regex CreateVolumeLevelRegex();
    private static readonly Regex VolumeLevelRegex = CreateVolumeLevelRegex();

    private const double NoteDurationMultiplier = 0.9;
    private static readonly TimeSpan TickDuration = TimeSpan.FromMilliseconds(1000d / 60); // 1000ms / 60fps (approximately 17 ms per tick)
    private static readonly TimeSpan PluckedStringsAudioClipDuration = TickDuration * AudioClipInfo.GetAudioClipLength(new Note { Instrument = Instrument.PluckedStrings, Number = 0 });

    static SpreadsheetReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // Required for reading from Excel spreadsheets from .NET Core apps
    }

    public static Song ReadSongFromSpreadsheet(string inputSpreadsheetFile, string spreadsheetTab)
    {
        if (inputSpreadsheetFile != null)
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.Copy(inputSpreadsheetFile, tempFile, true); // Use a temporary file so that we can load the data even if the original file is locked
                using var spreadsheetInputStream = File.OpenRead(tempFile);
                using var reader = ExcelReaderFactory.CreateReader(spreadsheetInputStream);

                do
                {
                    if (reader.Name == spreadsheetTab)
                    {
                        return ReadSongFromSpreadsheetTab(reader);
                    }
                }
                while (reader.NextResult());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        throw new Exception($"No songs were found in spreadsheet '{inputSpreadsheetFile}' tab '{spreadsheetTab}'");
    }

    private static Song ReadSongFromSpreadsheetTab(IExcelDataReader reader)
    {
        var row = 0;
        var currentLines = new List<List<List<List<SpreadsheetNote>>>>();
        var noteGroups = new List<NoteGroup>();
        var instrumentMappings = new List<InstrumentMapping>();
        var instrumentOffsets = new Dictionary<Instrument, int>();
        var instrumentVolumes = new Dictionary<Instrument, double>();
        var masterVolume = 1d;
        var beatsPerMinute = 60d;
        var isStartOfLine = true;
        var insertSpace = false;

        while (reader.Read())
        {
            row++;

            if (!reader.IsDBNull(0) && reader.GetFieldType(0) == typeof(double))
            {
                var noteNumber = (int)reader.GetDouble(0);
                var noteName = reader.GetString(1);
                var isDrum = Constants.Drums.Contains(noteName);
                List<List<List<SpreadsheetNote>>> notes;

                if (noteName == "Lyrics")
                {
                    notes = [.. Enumerable.Range(2, reader.FieldCount - 2)
                        .Select(column => StringUtils.SplitString(Convert.ToString(reader.GetValue(column)), '_')
                            .Select<string, List<SpreadsheetNote>>(lyric => !string.IsNullOrWhiteSpace(lyric) ?
                                [
                                    new()
                                    {
                                        Name = "Lyrics",
                                        Lyric = lyric
                                    }
                                ] : []
                            ).ToList()
                        )];
                }
                else
                {
                    notes = [.. Enumerable.Range(2, reader.FieldCount - 2)
                        .Select(column => StringUtils.SplitString(Convert.ToString(reader.GetValue(column)), '_')
                            .Select(rawNotes => rawNotes.Split(", ")
                                .Select(rawNote =>
                                {
                                    var noteMatch = SpreadsheetNoteRegex.Match(rawNote);
                                    if (noteMatch.Success)
                                    {
                                        var length = double.Parse(noteMatch.Groups[1].Value);
                                        var sharpOrFlat = noteMatch.Groups[2].Value;
                                        var instrumentIndicator = noteMatch.Groups[3].Value;

                                        var effectiveNoteNumber = sharpOrFlat switch { "#" => noteNumber + 1, "b" => noteNumber - 1, _ => noteNumber };
                                        var instrument = isDrum
                                            ? Instrument.Drumkit
                                            : instrumentIndicator switch
                                            {
                                                "P" => Instrument.Piano,
                                                "B" => Instrument.BassGuitar,
                                                "L" => Instrument.LeadGuitar,
                                                "W" => Instrument.Sawtooth,
                                                "Q" => Instrument.Square,
                                                "R" => Instrument.Celesta,
                                                "V" => Instrument.Vibraphone,
                                                "T" => Instrument.PluckedStrings,
                                                "S" => Instrument.SteelDrum,
                                                _ => instrumentMappings.FirstOrDefault(mapping => mapping.RangeStart <= effectiveNoteNumber && effectiveNoteNumber <= mapping.RangeEnd)?.Instrument ?? Instrument.Piano
                                            };
                                        var noteOffset = instrumentOffsets.TryGetValue(instrument, out var offsetValue) ? offsetValue : 0;
                                        var instrumentVolume = instrumentVolumes.TryGetValue(instrument, out var instrumentVolumeValue) ? instrumentVolumeValue : 1;

                                        return new SpreadsheetNote
                                        {
                                            Instrument = instrument,
                                            Number = effectiveNoteNumber + noteOffset,
                                            Name = isDrum ? noteName : $"{noteName[0]}{sharpOrFlat}{noteName.Substring(1)}",
                                            Volume = instrumentVolume * masterVolume,
                                            InverseLength = length
                                        };
                                    }
                                    else
                                    {
                                        return null;
                                    }
                                }).ToList()
                            ).ToList()
                        )];
                }

                currentLines.Add(notes);
            }
            else
            {
                ProcessSpreadsheetLines(currentLines, noteGroups, ref beatsPerMinute, ref isStartOfLine, ref insertSpace);

                if (!reader.IsDBNull(0) && reader.GetFieldType(0) == typeof(string))
                {
                    switch (reader.GetString(0))
                    {
                        case "Instrument Mappings":
                            foreach (var column in Enumerable.Range(1, reader.FieldCount - 1))
                            {
                                var value = reader.GetValue(column);
                                if (value == null)
                                {
                                    continue;
                                }

                                var match = InstrumentMappingRegex.Match(Convert.ToString(value));

                                if (match.Success)
                                {
                                    var instrument = ParseInstrument(match.Groups[1].Value);
                                    var rangeStart = int.Parse(match.Groups[2].Value);
                                    var rangeEnd = int.Parse(match.Groups[3].Value);

                                    instrumentMappings.Add(new InstrumentMapping { Instrument = instrument, RangeStart = rangeStart, RangeEnd = rangeEnd });
                                }
                                else
                                {
                                    Console.Error.WriteLine($"Unable to parse instrument mapping on sheet {reader.Name} row {row} column {column}");
                                }
                            }

                            break;
                        case "Instrument Offsets":
                            foreach (var column in Enumerable.Range(1, reader.FieldCount - 1))
                            {
                                var value = reader.GetValue(column);
                                if (value == null)
                                {
                                    continue;
                                }

                                var match = InstrumentOffsetRegex.Match(Convert.ToString(value));

                                if (match.Success)
                                {
                                    var instrument = ParseInstrument(match.Groups[1].Value);
                                    var offset = int.Parse(match.Groups[2].Value);

                                    instrumentOffsets[instrument] = offset;
                                }
                                else
                                {
                                    Console.Error.WriteLine($"Unable to parse instrument offset on sheet {reader.Name} row {row} column {column}");
                                }
                            }

                            break;
                        case "Volume":
                            foreach (var column in Enumerable.Range(1, reader.FieldCount - 1))
                            {
                                var value = reader.GetValue(column);
                                if (value == null)
                                {
                                    continue;
                                }

                                var match = VolumeLevelRegex.Match(Convert.ToString(value));

                                if (match.Success)
                                {
                                    var instrumentName = match.Groups[1].Value;
                                    var volume = double.Parse(match.Groups[2].Value) / 100;

                                    if (instrumentName.Length > 0)
                                    {
                                        var instrument = ParseInstrument(instrumentName);

                                        instrumentVolumes[instrument] = volume;
                                    }
                                    else
                                    {
                                        masterVolume = volume;
                                    }
                                }
                                else
                                {
                                    Console.Error.WriteLine($"Unable to parse volume level on sheet {reader.Name} row {row} column {column}");
                                }
                            }

                            break;
                    }
                }
            }
        }

        // Process any remaining lines
        ProcessSpreadsheetLines(currentLines, noteGroups, ref beatsPerMinute, ref isStartOfLine, ref insertSpace);

        return new Song
        {
            NoteGroups = noteGroups
        };
    }

    private static void ProcessSpreadsheetLines(List<List<List<List<SpreadsheetNote>>>> currentLines, List<NoteGroup> noteGroups, ref double beatsPerMinute, ref bool isStartOfLine, ref bool insertSpace)
    {
        if (currentLines.Count > 0)
        {
            var columnCount = currentLines.Max(line => line.Count);

            for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var noteListCount = currentLines.Max(line => line.ElementAtOrDefault(columnIndex)?.Count ?? 0);
                var allNotesInColumn = currentLines
                    .Select(line => line.ElementAtOrDefault(columnIndex) ?? [])
                    .SelectMany(noteLists => noteLists)
                    .SelectMany(noteList => noteList)
                    .Where(note => note != null && note.Number != 0)
                    .ToList();
                var inverseLength = allNotesInColumn.Count > 0 ? allNotesInColumn.Max(note => note.InverseLength) : 0;

                for (int noteListIndex = 0; noteListIndex < noteListCount; noteListIndex++)
                {
                    var notes = new List<Note>();
                    string lyric = null;

                    foreach (var line in currentLines)
                    {
                        foreach (var spreadsheetNote in line.ElementAtOrDefault(columnIndex)?.ElementAtOrDefault(noteListIndex) ?? [])
                        {
                            if (spreadsheetNote != null)
                            {
                                if (spreadsheetNote.Name == "Tempo")
                                {
                                    beatsPerMinute = (int)spreadsheetNote.InverseLength;
                                }
                                else if (spreadsheetNote.Name == "Lyrics")
                                {
                                    lyric = spreadsheetNote.Lyric;
                                }
                                else
                                {
                                    Note note = new()
                                    {
                                        Instrument = spreadsheetNote.Instrument,
                                        Number = spreadsheetNote.Number,
                                        Volume = spreadsheetNote.Volume,
                                        Duration = TimeSpan.FromMinutes(4 / spreadsheetNote.InverseLength / beatsPerMinute * NoteDurationMultiplier),
                                    };
                                    FixNoteDuration(note);
                                    notes.Add(note);
                                }
                            }
                        }
                    }

                    bool nextIsStartOfLine = false;

                    if (lyric is not null)
                    {
                        isStartOfLine |= lyric.StartsWith('|');
                        nextIsStartOfLine = lyric.EndsWith('|');

                        lyric = lyric.Trim('|');

                        if (insertSpace && !isStartOfLine)
                        {
                            lyric = " " + lyric;
                            insertSpace = false;
                        }

                        insertSpace = lyric.EndsWith(' ');
                        lyric = lyric.TrimEnd(' ');
                    }

                    if (inverseLength > 0)
                    {
                        noteGroups.Add(new NoteGroup { Notes = notes, Length = TimeSpan.FromMinutes(4 / inverseLength / beatsPerMinute), Lyrics = lyric, IsStartOfLine = lyric is not null && isStartOfLine });
                    }

                    if (lyric is not null)
                    {
                        isStartOfLine = nextIsStartOfLine;
                    }
                }
            }

            currentLines.Clear();
        }
    }

    private static void FixNoteDuration(Note note)
    {
        switch (note.Instrument)
        {
            case Instrument.Drumkit: // For drumkit notes, always use the actual length the audio clip since they should play exactly once to completion
                note.Duration = TickDuration * AudioClipInfo.GetAudioClipLength(note);
                break;
            case Instrument.PluckedStrings: // For plucked strings, limit the length to the actual length of the audio clip since sustained notes are disabled for that instrument
                if (note.Duration > PluckedStringsAudioClipDuration)
                {
                    note.Duration = PluckedStringsAudioClipDuration;
                }

                break;
        }
    }

    private static Instrument ParseInstrument(string text)
    {
        return Enum.TryParse(typeof(Instrument), text.Replace(" ", ""), true, out var instrumentValue) ? (Instrument)instrumentValue : throw new Exception($"Unknown instrument: {text}");
    }

    private class SpreadsheetNote
    {
        public Instrument Instrument { get; set; }

        /// <summary>
        /// Indicates the pitch of the note, from 1 to 48 (36 for some instruments).
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// The name of the note. Also supports the special values "Tempo" and "Lyrics".
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The volume of the note, from 0.0 to 1.0.
        /// </summary>
        public double Volume { get; set; }

        /// <summary>
        /// 1 for a whole note, 2 for a half note, 4 for a quarter note, etc.
        /// </summary>
        public double InverseLength { get; set; }

        public string Lyric { get; set; }
    }
}
