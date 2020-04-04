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
        private static readonly List<string> Drums = new List<string> { "kick-1", "kick-2", "snare-1", "snare-2", "snare-3", "high-hat-1", "high-hat-2" };
        private static readonly List<string> SpreadsheetDrums = new List<string> { "Kick 1", "Kick 2", "Snare 1", "Snare 2", "Snare 3", "High Hat 1", "High Hat 2" };
        private static readonly Regex NoteSignalRegex = new Regex(@"^signal-(\d|[A-JN])$");
        private static readonly Regex DrumSignalRegex = new Regex(@"^signal-([K-M])$");
        private static readonly Regex SpreadsheetNoteRegex = new Regex(@"^((?:\d|[.])+)([#b]?)([BR]?)$");

        private static readonly Dictionary<string, int> InstrumentOrder = new List<string> { "drum", "bass", "piano", "recorder" }
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
            var spreadsheetTab = configuration["SpreadsheetTab"];
            var defaultBeatsPerMinute = int.TryParse(configuration["DefaultBeatsPerMinute"], out var defaultBeatsPerMinuteValue) ? defaultBeatsPerMinuteValue : 60;

            var json = ReadBlueprintFileAsJson(inputBlueprintFile);
            var jsonObj = JsonSerializer.Deserialize<object>(json);
            var blueprintWrapper = JsonSerializer.Deserialize<BlueprintWrapper>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var memoryCells = blueprintWrapper.Blueprint.Entities
                .Where(entity => entity.Name == "constant-combinator")
                .OrderBy(entity => entity.Position.X - entity.Position.Y * 1000000)
                .ToList();

            WriteOutJson(outputJsonFile, jsonObj);
            WriteOutCommands(outputCommandsFile, memoryCells);

            if (inputSpreadsheetFile != null)
            {
                var tempFile = Path.GetTempFileName();
                try
                {
                    File.Copy(inputSpreadsheetFile, tempFile, true);
                    using var spreadsheetInputStream = File.OpenRead(tempFile);
                    using var reader = ExcelReaderFactory.CreateReader(spreadsheetInputStream);

                    var notFound = false;
                    while (reader.Name != spreadsheetTab)
                    {
                        if (!reader.NextResult())
                        {
                            notFound = true;
                            break;
                        }
                    }

                    if (!notFound)
                    {
                        var currentLines = new List<List<List<List<Note>>>>();
                        var currentAddress = baseAddress;
                        var noteGroups = new List<NoteGroup>();

                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(0) && reader.GetFieldType(0) == typeof(double))
                            {
                                var noteNumber = (int)reader.GetDouble(0);
                                var noteName = reader.GetString(1);
                                var isDrum = SpreadsheetDrums.Contains(noteName);

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

                                                    var instrument = isDrum ? "drum" : instrumentIndicator switch { "L" => "lead", "B" => "bass", "R" => "recorder", _ => "piano" };
                                                    var effectiveNoteNumber = sharpOrFlat switch { "#" => noteNumber + 1, "b" => noteNumber - 1, _ => noteNumber };
                                                    var effectiveLength = (int)Math.Ceiling(length);

                                                    return new Note
                                                    {
                                                        Instrument = instrument,
                                                        Number = effectiveNoteNumber,
                                                        Name = isDrum ? noteName : $"{noteName[0]}{sharpOrFlat}{noteName.Substring(1)}",
                                                        Length = length,
                                                        EffectiveLength = effectiveLength
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
                                            .Where(note => note != null)
                                            .ToList();
                                        var length = allNotesInColumn.Count > 0 ? allNotesInColumn.Max(note => note.EffectiveLength) : 0;

                                        for (int noteListIndex = 0; noteListIndex < noteListCount; noteListIndex++, currentAddress++)
                                        {
                                            var notes = new List<Note>();

                                            foreach (var line in currentLines)
                                            {
                                                foreach (var note in line.ElementAtOrDefault(columnIndex)?.ElementAtOrDefault(noteListIndex) ?? new List<Note>())
                                                {
                                                    if (note != null)
                                                    {
                                                        notes.Add(note);
                                                    }
                                                }
                                            }

                                            noteGroups.Add(new NoteGroup { Address = currentAddress, Notes = notes, Length = length });
                                        }
                                    }

                                    currentLines.Clear();
                                }
                            }
                        }

                        int? currentLength = null;
                        int? currentBeatsPerMinute = null;

                        foreach (var noteGroup in noteGroups)
                        {
                            var currentSignals = new Dictionary<string, char>
                            {
                                { "piano", '0' },
                                { "lead", 'A' },
                                { "bass", 'G' },
                                { "drum", 'K' },
                                { "recorder", 'N' }
                            };

                            var filters = noteGroup.Notes
                                .OrderBy(note => InstrumentOrder[note.Instrument] * 100 + note.Number)
                                .Select(note => CreateFilter(currentSignals[note.Instrument]++, note.Number))
                                .ToList();

                            if (noteGroup.Length != currentLength)
                            {
                                filters.Add(CreateFilter('Z', noteGroup.Length));
                                currentLength = noteGroup.Length;
                            }

                            if (defaultBeatsPerMinute != currentBeatsPerMinute)
                            {
                                filters.Add(CreateFilter('Y', defaultBeatsPerMinute));
                                currentBeatsPerMinute = defaultBeatsPerMinute;
                            }

                            for (int index = 0; index < filters.Count; index++)
                            {
                                filters[index].Index = index + 1;
                            }

                            memoryCells[noteGroup.Address].Control_behavior = new ControlBehavior { Filters = filters };
                        }
                    }
                }
                finally
                {
                    File.Delete(tempFile);
                }

                WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
                WriteOutJson(outputUpdatedJsonFile, blueprintWrapper);
                WriteOutCommands(outputUpdatedCommandsFile, memoryCells);
            }
        }

        private static string ReadBlueprintFileAsJson(string blueprintFile)
        {
            using var inputStream = new StreamReader(blueprintFile);
            var input = inputStream.ReadToEnd().Trim();

            var compressedBytes = Convert.FromBase64String(input.Substring(1));

            var buffer = new MemoryStream();
            using (var output = new ZOutputStream(buffer))
            {
                output.Write(compressedBytes);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        private static void WriteBlueprintFileFromJson(string blueprintFile, string json)
        {
            var buffer = new MemoryStream();
            using (var output = new ZOutputStream(buffer, 9))
            {
                output.Write(Encoding.UTF8.GetBytes(json));
            }

            using var outputWriter = new StreamWriter(blueprintFile);
            outputWriter.Write('0');
            outputWriter.Write(Convert.ToBase64String(buffer.ToArray()));
            outputWriter.Flush();
        }

        private static void WriteOutBlueprint(string blueprintFile, BlueprintWrapper wrapper)
        {
            if (blueprintFile == null)
            {
                return;
            }

            WriteBlueprintFileFromJson(blueprintFile, JsonSerializer.Serialize(wrapper, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                IgnoreNullValues = true
            }));
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

                                string instrument;
                                if (signal >= '0' && signal <= '9')
                                {
                                    instrument = "piano";
                                }
                                else if (signal >= 'A' && signal <= 'F')
                                {
                                    instrument = "lead";
                                }
                                else if (signal >= 'G' && signal <= 'J')
                                {
                                    instrument = "bass";
                                }
                                else if (signal == 'N')
                                {
                                    instrument = "celesta";
                                }
                                else
                                {
                                    instrument = "unknown";
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
                                return $"{filter.Count} beats per minute";
                            }
                            else if (signalName == "signal-Z")
                            {
                                return $"duration 1/{filter.Count}";
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

        private static Filter CreateFilter(char signal, int count)
        {
            return new Filter { Signal = new SignalID { Name = $"signal-{signal}", Type = "virtual" }, Count = count };
        }

        private class Note
        {
            public string Instrument { get; set; }
            public int Number { get; set; }
            public string Name { get; set; }
            public double Length { get; set; }
            public int EffectiveLength { get; set; }
        }

        private class NoteGroup
        {
            public int Address { get; set; }
            public List<Note> Notes { get; set; }
            public int Length { get; set; }
        }
    }
}
