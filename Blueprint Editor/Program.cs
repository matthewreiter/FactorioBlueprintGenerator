using BlueprintCommon;
using BlueprintEditor.Models;
using ExcelDataReader;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using zlib;

namespace BlueprintEditor
{
    public class Program
    {
        private static readonly List<string> Notes = new List<string> { "F", "F#", "G", "G#", "A", "A#", "B", "C", "C#", "D", "D#", "E" };
        private static readonly List<string> Drums = new List<string> { "Kick 1", "Kick 2", "Snare 1", "Snare 2", "Snare 3", "High Hat 1", "High Hat 2" };
        private static readonly Regex NoteSignalRegex = new Regex(@"^signal-(\d|[A-JN-R])$");
        private static readonly Regex DrumSignalRegex = new Regex(@"^signal-([K-M])$");
        private static readonly Regex SpreadsheetNoteRegex = new Regex(@"^((?:\d|[.])+)([#b]?)([BR]?)$");
        private static readonly Regex InstrumentMappingRegex = new Regex(@"^(.*?): (\d+)-(\d+)$");
        private static readonly Regex InstrumentOffsetRegex = new Regex(@"^(.*?): (-?\d+)$");

        private static readonly Dictionary<Instrument, int> InstrumentOrder = new List<Instrument> { Instrument.Drum, Instrument.BassGuitar, Instrument.LeadGuitar, Instrument.Piano, Instrument.Celesta, Instrument.SteelDrum }
            .Select((instrument, index) => new { instrument, index })
            .ToDictionary(instrumentWithIndex => instrumentWithIndex.instrument, instrumentWithIndex => instrumentWithIndex.index);

        public static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            var inputBlueprintFile = configuration["InputBlueprint"];
            var inputSpreadsheetFile = configuration["InputSpreadsheet"];
            var outputBlueprintFile = configuration["OutputBlueprint"];
            var outputJsonFile = configuration["OutputJson"];
            var outputUpdatedJsonFile = configuration["OutputUpdatedJson"];
            var outputCommandsFile = configuration["OutputCommands"];
            var outputUpdatedCommandsFile = configuration["OutputUpdatedCommands"];
            var baseAddress = int.TryParse(configuration["BaseAddress"], out var baseAddressValue) ? baseAddressValue : 0;
            var songAlignment = int.TryParse(configuration["SongAlignment"], out var songAlignmentValue) ? songAlignmentValue : 1;
            var spreadsheetTabs = SplitString(configuration["SpreadsheetTabs"], ',');

            var json = BlueprintUtil.ReadBlueprintFileAsJson(inputBlueprintFile);
            var jsonObj = JsonSerializer.Deserialize<object>(json);
            var blueprintWrapper = JsonSerializer.Deserialize<BlueprintWrapper>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var memoryCells = blueprintWrapper.Blueprint.Entities
                .Where(entity => entity.Name == "constant-combinator")
                .OrderBy(entity => entity.Position.X - entity.Position.Y * 1000000)
                .ToList();

            WriteOutJson(outputJsonFile, jsonObj);
            WriteOutCommands(outputCommandsFile, memoryCells);

            var songs = ReadSongsFromSpreadsheet(inputSpreadsheetFile, spreadsheetTabs);

            UpdateMemoryCellsFromSongs(memoryCells, songs, baseAddress, songAlignment);

            BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
            WriteOutJson(outputUpdatedJsonFile, blueprintWrapper);
            WriteOutCommands(outputUpdatedCommandsFile, memoryCells);
        }

        private static List<List<NoteGroup>> ReadSongsFromSpreadsheet(string inputSpreadsheetFile, string[] spreadsheetTabs)
        {
            var songs = new List<List<NoteGroup>>();

            if (inputSpreadsheetFile != null)
            {
                var tempFile = Path.GetTempFileName();
                try
                {
                    File.Copy(inputSpreadsheetFile, tempFile, true);
                    using var spreadsheetInputStream = File.OpenRead(tempFile);
                    using var reader = ExcelReaderFactory.CreateReader(spreadsheetInputStream);

                    while (reader.NextResult())
                    {
                        if (spreadsheetTabs.Contains(reader.Name))
                        {
                            songs.Add(ReadSongFromSpreadsheet(reader));
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

        private static List<NoteGroup> ReadSongFromSpreadsheet(IExcelDataReader reader)
        {
            var row = 0;
            var currentLines = new List<List<List<List<Note>>>>();
            var noteGroups = new List<NoteGroup>();
            var instrumentMappings = new List<InstrumentMapping>();
            var instrumentOffsets = new Dictionary<Instrument, int>();

            while (reader.Read())
            {
                row++;

                if (!reader.IsDBNull(0) && reader.GetFieldType(0) == typeof(double))
                {
                    var noteNumber = (int)reader.GetDouble(0);
                    var noteName = reader.GetString(1);
                    var isDrum = Drums.Contains(noteName);

                    var notes = Enumerable.Range(2, reader.FieldCount - 2)
                        .Select(column => SplitString(Convert.ToString(reader.GetValue(column)), '_')
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
                                foreach (var column in Enumerable.Range(2, reader.FieldCount - 2))
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
                                foreach (var column in Enumerable.Range(2, reader.FieldCount - 2))
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
                        }
                    }
                }
            }

            // Process any remaining lines
            ProcessSpreadsheetLines(currentLines, noteGroups);

            return noteGroups;
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

        private static void UpdateMemoryCellsFromSongs(List<Entity> memoryCells, List<List<NoteGroup>> songs, int baseAddress, int songAlignment)
        {
            var currentAddress = baseAddress;

            Filter CreateJumpFilter(int targetAddress)
            {
                return CreateFilter('U', targetAddress - (currentAddress + 1));
            }

            void UpdateMemoryCell(List<Filter> filters, bool isEnabled = true)
            {
                for (int index = 0; index < filters.Count; index++)
                {
                    filters[index].Index = index + 1;
                }

                memoryCells[currentAddress++].Control_behavior = new ControlBehavior { Filters = filters, Is_on = isEnabled ? (bool?)null : false };
            }

            void ClearMemoryCell()
            {
                memoryCells[currentAddress++].Control_behavior = null;
            }

            foreach (var noteGroups in songs)
            {
                var songAddress = currentAddress;
                int currentBeatsPerMinute = 60;

                foreach (var noteGroup in noteGroups)
                {
                    if (noteGroup.BeatsPerMinute.HasValue)
                    {
                        currentBeatsPerMinute = noteGroup.BeatsPerMinute.Value;
                    }

                    var currentSignals = new Dictionary<Instrument, char>
                            {
                                { Instrument.Piano, '0' },
                                { Instrument.LeadGuitar, 'A' },
                                { Instrument.BassGuitar, 'G' },
                                { Instrument.Drum, 'K' },
                                { Instrument.Celesta, 'N' },
                                { Instrument.SteelDrum, 'O' }
                            };

                    var filters = noteGroup.Notes
                        .OrderBy(note => InstrumentOrder[note.Instrument] * 100 + note.Number)
                        .Select(note => CreateFilter(currentSignals[note.Instrument]++, note.Number))
                        .Append(CreateFilter('Z', (long)(14400 / currentBeatsPerMinute / noteGroup.Length)))
                        .Append(CreateFilter('Y', GetHistogram(noteGroup.Notes)))
                        .ToList();

                    UpdateMemoryCell(filters);
                }

                // Create a disabled jump back to the beginning of the song
                UpdateMemoryCell(new List<Filter> { CreateJumpFilter(songAddress) }, isEnabled: false);

                // Jump to the next song, which starts at the beginning of the next line of memory
                var nextSongAddress = (currentAddress / songAlignment + 1) * songAlignment;
                UpdateMemoryCell(new List<Filter> { CreateJumpFilter(nextSongAddress) });

                // Blank all memory up to the next song
                while (currentAddress < nextSongAddress)
                {
                    ClearMemoryCell();
                }
            }

            // Jump back to the beginning
            UpdateMemoryCell(new List<Filter> { CreateJumpFilter(0) });
        }

        private static long GetHistogram(List<Note> notes)
        {
            return notes
                .GroupBy(note => note.Pitch / 5)
                .Select(group => Math.Min(group.Count(), 3) << (group.Key * 2))
                .Sum();
        }

        private static void WriteOutJson(string outputJsonFile, object jsonObj)
        {
            if (outputJsonFile == null)
            {
                return;
            }

            using var outputStream = new StreamWriter(outputJsonFile);
            outputStream.Write(JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                IgnoreNullValues = true
            }));
        }

        private static void WriteOutCommands(string outputCommandsFile, List<Entity> memoryCells)
        {
            if (outputCommandsFile == null)
            {
                return;
            }

            var commands = memoryCells
                .Select((entity, index) => new { entity, index })
                .Where(entityWithIndex => entityWithIndex.entity.Control_behavior != null)
                .Select(entityWithIndex =>
                {
                    var entity = entityWithIndex.entity;
                    var address = entityWithIndex.index;
                    var isEnabled = entity.Control_behavior.Is_on ?? true;

                    var signals = entity.Control_behavior.Filters
                        .Select(filter =>
                        {
                            var signalName = filter.Signal.Name;

                            Match match;
                            if ((match = NoteSignalRegex.Match(signalName)).Success)
                            {
                                var signal = match.Groups[1].Value[0];

                                Instrument instrument;
                                if (signal >= '0' && signal <= '9')
                                {
                                    instrument = Instrument.Piano;
                                }
                                else if (signal >= 'A' && signal <= 'F')
                                {
                                    instrument = Instrument.LeadGuitar;
                                }
                                else if (signal >= 'G' && signal <= 'J')
                                {
                                    instrument = Instrument.BassGuitar;
                                }
                                else if (signal == 'N')
                                {
                                    instrument = Instrument.Celesta;
                                }
                                else if (signal >= 'O' && signal <= 'R')
                                {
                                    instrument = Instrument.SteelDrum;
                                }
                                else
                                {
                                    instrument = Instrument.Unknown;
                                }

                                var note = (int)(filter.Count - 1);
                                return $"{instrument} {Notes[note % Notes.Count]}{(note + 8) / Notes.Count + 1}";
                            }
                            else if ((match = DrumSignalRegex.Match(signalName)).Success)
                            {
                                var drum = (int)(filter.Count - 1);
                                return Drums[drum];
                            }
                            else if (signalName == "signal-U")
                            {
                                return $"jump by {filter.Count} to {address + 1 + filter.Count}";
                            }
                            else if (signalName == "signal-Y")
                            {
                                return $"histogram {filter.Count:X}";
                            }
                            else if (signalName == "signal-Z")
                            {
                                return $"duration {filter.Count / 60f}";
                            }
                            else
                            {
                                return null;
                            }
                        })
                        .Where(note => note != null);

                    return $"{address:D4}: {string.Join(", ", signals)}{(!isEnabled ? " (disabled)" : "")}";
                });

            using var outputStream = new StreamWriter(outputCommandsFile);
            foreach (var command in commands)
            {
                outputStream.WriteLine(command);
            }
        }

        private static string[] SplitString(string value, char separator)
        {
            return string.IsNullOrWhiteSpace(value) ? new string[] { } : value.Split(separator);
        }

        private static Filter CreateFilter(char signal, long count)
        {
            return new Filter { Signal = new SignalID { Name = $"signal-{signal}", Type = "virtual" }, Count = count };
        }

        private static Instrument ParseInstrument(string text)
        {
            return Enum.TryParse(typeof(Instrument), text.Replace(" ", ""), true, out var instrumentValue) ? (Instrument)instrumentValue : Instrument.Unknown;
        }

        private class Note
        {
            public Instrument Instrument { get; set; }
            public int Number { get; set; }
            public int Pitch { get; set; }
            public string Name { get; set; }
            public double Length { get; set; }
        }

        private class NoteGroup
        {
            public List<Note> Notes { get; set; }
            public double Length { get; set; }
            public int? BeatsPerMinute { get; set; }
        }

        private class InstrumentMapping
        {
            public Instrument Instrument { get; set; }
            public int RangeStart { get; set; }
            public int RangeEnd { get; set; }
        }

        private enum Instrument
        {
            Unknown,
            Piano,
            LeadGuitar,
            BassGuitar,
            Drum,
            SteelDrum,
            Celesta
        }
    }
}
