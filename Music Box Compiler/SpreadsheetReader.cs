﻿using ExcelDataReader;
using MusicBoxCompiler.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MusicBoxCompiler;

public static class SpreadsheetReader
{
    private static readonly Regex SpreadsheetNoteRegex = new Regex(@"^((?:\d|[.])+)([#b]?)([BR]?)$");
    private static readonly Regex InstrumentMappingRegex = new Regex(@"^(.+?): (\d+)-(\d+)$");
    private static readonly Regex InstrumentOffsetRegex = new Regex(@"^(.+?): (-?\d+)$");
    private static readonly Regex VolumeLevelRegex = new Regex(@"^(?:(.+?): )?(-?\d+(?:\.\d+)?)$");

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
        var currentLines = new List<List<List<List<Note>>>>();
        var noteGroups = new List<NoteGroup>();
        var instrumentMappings = new List<InstrumentMapping>();
        var instrumentOffsets = new Dictionary<Instrument, int>();
        var instrumentVolumes = new Dictionary<Instrument, double>();
        var masterVolume = 1d;
        var beatsPerMinute = 60d;

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

                                    return new Note
                                    {
                                        Instrument = instrument,
                                        Number = effectiveNoteNumber + noteOffset,
                                        Name = isDrum ? noteName : $"{noteName[0]}{sharpOrFlat}{noteName.Substring(1)}",
                                        Volume = instrumentVolume * masterVolume,
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
                ProcessSpreadsheetLines(currentLines, noteGroups, ref beatsPerMinute);

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
        ProcessSpreadsheetLines(currentLines, noteGroups, ref beatsPerMinute);

        return new Song
        {
            NoteGroups = noteGroups
        };
    }

    private static void ProcessSpreadsheetLines(List<List<List<List<Note>>>> currentLines, List<NoteGroup> noteGroups, ref double beatsPerMinute)
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
                var length = allNotesInColumn.Count > 0 ? allNotesInColumn.Max(note => note.Length) : 0;

                for (int noteListIndex = 0; noteListIndex < noteListCount; noteListIndex++)
                {
                    var notes = new List<Note>();

                    foreach (var line in currentLines)
                    {
                        foreach (var note in line.ElementAtOrDefault(columnIndex)?.ElementAtOrDefault(noteListIndex) ?? [])
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

                    noteGroups.Add(new NoteGroup { Notes = notes, Length = TimeSpan.FromMinutes(4 / length / beatsPerMinute) });
                }
            }

            currentLines.Clear();
        }
    }

    private static Instrument ParseInstrument(string text)
    {
        return Enum.TryParse(typeof(Instrument), text.Replace(" ", ""), true, out var instrumentValue) ? (Instrument)instrumentValue : throw new Exception($"Unknown instrument: {text}");
    }
}
