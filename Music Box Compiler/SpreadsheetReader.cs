using ExcelDataReader;
using MusicBoxCompiler.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MusicBoxCompiler
{
    public static class SpreadsheetReader
    {
        private static readonly Regex SpreadsheetNoteRegex = new Regex(@"^((?:\d|[.])+)([#b]?)([BR]?)$");
        private static readonly Regex InstrumentMappingRegex = new Regex(@"^(.*?): (\d+)-(\d+)$");
        private static readonly Regex InstrumentOffsetRegex = new Regex(@"^(.*?): (-?\d+)$");

        static SpreadsheetReader()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // Required for reading from Excel spreadsheets from .NET Core apps
        }

        public static List<List<NoteGroup>> ReadSongsFromSpreadsheet(string inputSpreadsheetFile, string[] spreadsheetTabs, StreamWriter midiEventWriter)
        {
            var songs = new List<List<NoteGroup>>();

            if (inputSpreadsheetFile != null)
            {
                var tempFile = Path.GetTempFileName();
                try
                {
                    File.Copy(inputSpreadsheetFile, tempFile, true); // Use a temporary file so that we can load the data even if the original file is locked
                    using var spreadsheetInputStream = File.OpenRead(tempFile);
                    using var reader = ExcelReaderFactory.CreateReader(spreadsheetInputStream);

                    while (reader.NextResult())
                    {
                        if (spreadsheetTabs.Contains(reader.Name))
                        {
                            songs.AddRange(ReadSongsFromSpreadsheetTab(reader, midiEventWriter));
                        }
                    }
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }

            return songs;
        }

        private static List<List<NoteGroup>> ReadSongsFromSpreadsheetTab(IExcelDataReader reader, StreamWriter midiEventWriter)
        {
            var row = 0;
            var currentLines = new List<List<List<List<Note>>>>();
            var noteGroups = new List<NoteGroup>();
            var instrumentMappings = new List<InstrumentMapping>();
            var instrumentOffsets = new Dictionary<Instrument, int>();
            var midiFiles = new List<string>();

            while (reader.Read())
            {
                row++;

                if (!reader.IsDBNull(0) && reader.GetFieldType(0) == typeof(double))
                {
                    var noteNumber = (int)reader.GetDouble(0);
                    var noteName = reader.GetString(1);
                    var isDrum = Constants.Drums.Contains(noteName);

                    var notes = Enumerable.Range(2, reader.FieldCount - 2)
                        .Select(column => StringUtil.SplitString(Convert.ToString(reader.GetValue(column)), '_')
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
                                            ? Instrument.Drum
                                            : instrumentIndicator switch
                                            {
                                                "L" => Instrument.LeadGuitar,
                                                "B" => Instrument.BassGuitar,
                                                "R" => Instrument.Celesta,
                                                "S" => Instrument.SteelDrum,
                                                "P" => Instrument.Piano,
                                                _ => instrumentMappings.FirstOrDefault(mapping => mapping.RangeStart <= effectiveNoteNumber && effectiveNoteNumber <= mapping.RangeEnd)?.Instrument ?? Instrument.Piano
                                            };
                                        var noteOffset = instrumentOffsets.TryGetValue(instrument, out var offsetValue) ? offsetValue : 0;

                                        return new Note
                                        {
                                            Instrument = instrument,
                                            Number = effectiveNoteNumber + noteOffset,
                                            Pitch = isDrum ? effectiveNoteNumber * 5 : effectiveNoteNumber,
                                            Name = isDrum ? noteName : $"{noteName[0]}{sharpOrFlat}{noteName.Substring(1)}",
                                            Length = length
                                        };
                                    }
                                    else
                                    {
                                        return null;
                                    }
                                }).ToList()
                            ).ToList()
                        ).ToList();

                    currentLines.Add(notes);
                }
                else
                {
                    ProcessSpreadsheetLines(currentLines, noteGroups);

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
                            case "Midi File":
                                foreach (var column in Enumerable.Range(1, reader.FieldCount - 1))
                                {
                                    var value = reader.GetValue(column);
                                    if (value == null)
                                    {
                                        continue;
                                    }

                                    midiFiles.Add(Convert.ToString(value));
                                }

                                break;
                        }
                    }
                }
            }

            var songs = new List<List<NoteGroup>>();

            if (noteGroups.Count > 0)
            {
                songs.Add(noteGroups);
            }

            // Process any remaining lines
            ProcessSpreadsheetLines(currentLines, noteGroups);

            foreach (var midiFile in midiFiles)
            {
                songs.Add(MidiReader.ReadSong(midiFile, midiEventWriter, instrumentOffsets));
            }

            return songs;
        }

        private static void ProcessSpreadsheetLines(List<List<List<List<Note>>>> currentLines, List<NoteGroup> noteGroups)
        {
            if (currentLines.Count > 0)
            {
                var columnCount = currentLines.Max(line => line.Count);

                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    var noteListCount = currentLines.Max(line => line.ElementAtOrDefault(columnIndex)?.Count ?? 0);
                    var allNotesInColumn = currentLines
                        .Select(line => line.ElementAtOrDefault(columnIndex) ?? new List<List<Note>>())
                        .SelectMany(noteLists => noteLists)
                        .SelectMany(noteList => noteList)
                        .Where(note => note != null && note.Number != 0)
                        .ToList();
                    var length = allNotesInColumn.Count > 0 ? allNotesInColumn.Max(note => note.Length) : 0;

                    for (int noteListIndex = 0; noteListIndex < noteListCount; noteListIndex++)
                    {
                        var notes = new List<Note>();
                        int? beatsPerMinute = null;

                        foreach (var line in currentLines)
                        {
                            foreach (var note in line.ElementAtOrDefault(columnIndex)?.ElementAtOrDefault(noteListIndex) ?? new List<Note>())
                            {
                                if (note != null)
                                {
                                    if (note.Name == "Tempo")
                                    {
                                        beatsPerMinute = (int)note.Length;
                                    }
                                    else
                                    {
                                        notes.Add(note);
                                    }
                                }
                            }
                        }

                        noteGroups.Add(new NoteGroup { Notes = notes, Length = length, BeatsPerMinute = beatsPerMinute });
                    }
                }

                currentLines.Clear();
            }
        }

        private static Instrument ParseInstrument(string text)
        {
            return Enum.TryParse(typeof(Instrument), text.Replace(" ", ""), true, out var instrumentValue) ? (Instrument)instrumentValue : Instrument.Unknown;
        }
    }
}
