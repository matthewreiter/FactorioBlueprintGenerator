using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using MemoryInitializer;
using Microsoft.Extensions.Configuration;
using MusicBoxCompiler.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MusicBoxCompiler
{
    public static class MusicBoxCompiler
    {
        private static readonly List<string> Notes = new List<string> { "F", "F#", "G", "G#", "A", "A#", "B", "C", "C#", "D", "D#", "E" };
        private static readonly Regex NoteSignalRegex = new Regex(@"^signal-(\d|[A-JN-R])$");
        private static readonly Regex DrumSignalRegex = new Regex(@"^signal-([K-M])$");

        private static readonly Dictionary<Instrument, int> InstrumentOrder = new List<Instrument> { Instrument.Drum, Instrument.BassGuitar, Instrument.LeadGuitar, Instrument.Piano, Instrument.Celesta, Instrument.SteelDrum }
            .Select((instrument, index) => new { instrument, index })
            .ToDictionary(instrumentWithIndex => instrumentWithIndex.instrument, instrumentWithIndex => instrumentWithIndex.index);

        public static void Run(IConfigurationRoot configuration)
        {
            Run(configuration.Get<MusicBoxConfiguration>());
        }

        public static void Run(MusicBoxConfiguration configuration)
        {
            var inputSpreadsheetFile = configuration.InputSpreadsheet;
            var inputMidiFiles = StringUtil.SplitString(configuration.InputMidiFiles, ',');
            var outputBlueprintFile = configuration.OutputBlueprint;
            var outputJsonFile = configuration.OutputJson;
            var outputCommandsFile = configuration.OutputCommands;
            var outputMidiEventsFile = configuration.OutputMidiEvents;
            var baseAddress = configuration.BaseAddress ?? 0;
            var width = configuration.Width ?? 16;
            var height = configuration.Height ?? 16;
            var spreadsheetTabs = StringUtil.SplitString(configuration.SpreadsheetTabs, ',');

            using var midiEventWriter = outputMidiEventsFile != null ? new StreamWriter(outputMidiEventsFile) : null;
            var songs = SpreadsheetReader.ReadSongsFromSpreadsheet(inputSpreadsheetFile, spreadsheetTabs, midiEventWriter)
                .Concat(inputMidiFiles.Select(midiFile => MidiReader.ReadSong(midiFile, midiEventWriter)))
                .ToList();

            var blueprint = CreateBlueprintFromSongs(songs, baseAddress, width, height);
            BlueprintUtil.PopulateIndices(blueprint);

            var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };
            var memoryCells = blueprint.Entities
                .Where(entity => entity.Name == ItemNames.ConstantCombinator)
                .OrderBy(entity => entity.Position.X - entity.Position.Y * 1000000)
                .ToList();

            BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
            BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
            WriteOutCommands(outputCommandsFile, memoryCells);
        }

        private static Blueprint CreateBlueprintFromSongs(List<List<NoteGroup>> songs, int baseAddress, int width, int height)
        {
            var memoryCells = new List<MemoryCell>();
            var currentAddress = baseAddress;

            Filter CreateJumpFilter(int targetAddress)
            {
                return CreateFilter('U', targetAddress - (currentAddress + 1));
            }

            void AddMemoryCell(List<Filter> filters, bool isEnabled = true)
            {
                memoryCells.Add(new MemoryCell { Address = currentAddress++, Filters = filters, IsEnabled = isEnabled });
            }

            void ClearMemoryCell()
            {
                AddMemoryCell(null);
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
                        .Append(CreateFilter('Z', (int)(14400 / currentBeatsPerMinute / noteGroup.Length)))
                        .Append(CreateFilter('Y', GetHistogram(noteGroup.Notes)))
                        .ToList();

                    AddMemoryCell(filters);
                }

                // Create a disabled jump back to the beginning of the song
                AddMemoryCell(new List<Filter> { CreateJumpFilter(songAddress) }, isEnabled: false);

                // Jump to the next song, which starts at the beginning of the next line of memory
                var nextSongAddress = (currentAddress / width + 1) * width;
                AddMemoryCell(new List<Filter> { CreateJumpFilter(nextSongAddress) });

                // Blank all memory up to the next song
                while (currentAddress < nextSongAddress)
                {
                    ClearMemoryCell();
                }
            }

            // Jump back to the beginning
            AddMemoryCell(new List<Filter> { CreateJumpFilter(0) });

            var romUsed = memoryCells.Count;
            var totalRom = width * height;

            Console.WriteLine($"ROM usage: {romUsed}/{totalRom} ({(double)romUsed / totalRom * 100:F1}%)");

            return RomGenerator.Generate(new RomConfiguration
            {
                Width = width,
                Height = height,
                ProgramRows = height,
                ProgramName = "Songs",
                IconItemNames = new List<string> { ItemNames.ElectronicCircuit, ItemNames.ProgrammableSpeaker }
            }, memoryCells);
        }

        private static int GetHistogram(List<Note> notes)
        {
            return notes
                .GroupBy(note => note.Pitch / 5)
                .Select(group => Math.Min(group.Count(), 3) << (group.Key * 2))
                .Sum();
        }

        private static void WriteOutCommands(string outputCommandsFile, List<Entity> memoryCells)
        {
            if (outputCommandsFile == null)
            {
                return;
            }

            var commands = memoryCells
                .Select((entity, index) => (entity, index))
                .Where(entityWithIndex => entityWithIndex.entity.Control_behavior != null)
                .Select(entityWithIndex =>
                {
                    var (entity, address) = entityWithIndex;
                    var isEnabled = entity.Control_behavior?.Is_on ?? true;

                    var signals = entity.Control_behavior?.Filters
                        ?.Select(filter =>
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

                                var note = filter.Count - 1;
                                return $"{instrument} {Notes[note % Notes.Count]}{(note + 5) / Notes.Count + 1}";
                            }
                            else if ((match = DrumSignalRegex.Match(signalName)).Success)
                            {
                                var drum = filter.Count - 1;
                                return Constants.Drums[drum];
                            }
                            else if (signalName == VirtualSignalNames.LetterOrDigit('U'))
                            {
                                return $"jump by {filter.Count} to {address + 1 + filter.Count}";
                            }
                            else if (signalName == VirtualSignalNames.LetterOrDigit('Y'))
                            {
                                return $"histogram {filter.Count:X}";
                            }
                            else if (signalName == VirtualSignalNames.LetterOrDigit('Z'))
                            {
                                return $"duration {filter.Count / 60f}";
                            }
                            else
                            {
                                return null;
                            }
                        })
                        ?.Where(note => note != null);

                    return signals != null ? $"{address:D4}: {string.Join(", ", signals)}{(!isEnabled ? " (disabled)" : "")}" : null;
                })
                .Where(command => command != null);

            using var outputStream = new StreamWriter(outputCommandsFile);
            foreach (var command in commands)
            {
                outputStream.WriteLine(command);
            }
        }

        private static Filter CreateFilter(char signal, int count)
        {
            return new Filter { Signal = new SignalID { Name = VirtualSignalNames.LetterOrDigit(signal), Type = SignalTypes.Virtual }, Count = count };
        }
    }

    public class MusicBoxConfiguration
    {
        public string InputSpreadsheet { get; set; }
        public string InputMidiFiles { get; set; }
        public string OutputBlueprint { get; set; }
        public string OutputJson { get; set; }
        public string OutputCommands { get; set; }
        public string OutputMidiEvents { get; set; }
        public int? BaseAddress { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string SpreadsheetTabs { get; set; }
    }
}
