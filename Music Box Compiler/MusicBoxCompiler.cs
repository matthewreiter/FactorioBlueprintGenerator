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
using System.Text.RegularExpressions;

namespace MusicBoxCompiler
{
    public static class MusicBoxCompiler
    {
        private const int InstrumentCount = 12;
        private static readonly List<string> Notes = new List<string> { "F", "F#", "G", "G#", "A", "A#", "B", "C", "C#", "D", "D#", "E" };
        private static readonly Regex NoteSignalRegex = new Regex(@"^signal-(\d)$");

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
            var volumeLevels = configuration.VolumeLevels ?? 10;
            var minVolume = configuration.MinVolume ?? 0.1;
            var maxVolume = configuration.MaxVolume ?? 1;
            var spreadsheetTabs = StringUtil.SplitString(configuration.SpreadsheetTabs, ',');

            using var midiEventWriter = outputMidiEventsFile != null ? new StreamWriter(outputMidiEventsFile) : null;
            var songs = SpreadsheetReader.ReadSongsFromSpreadsheet(inputSpreadsheetFile, spreadsheetTabs, midiEventWriter)
                .Concat(inputMidiFiles.Select(midiFile => MidiReader.ReadSong(midiFile, midiEventWriter)))
                .ToList();

            var blueprint = CreateBlueprintFromSongs(songs, baseAddress, width, height, volumeLevels, minVolume, maxVolume);
            BlueprintUtil.PopulateIndices(blueprint);

            var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };
            var memoryCells = blueprint.Entities
                .Where(entity => entity.Name == ItemNames.ConstantCombinator)
                .OrderBy(entity => entity.Position.X - entity.Position.Y * 1000000)
                .ToList();

            BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
            BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
            WriteOutCommands(outputCommandsFile, memoryCells, volumeLevels, minVolume, maxVolume);
        }

        private static Blueprint CreateBlueprintFromSongs(List<List<NoteGroup>> songs, int baseAddress, int width, int height, int volumeLevels, double minVolume, double maxVolume)
        {
            var memoryCells = new List<MemoryCell>();
            var currentAddress = baseAddress;

            Filter CreateJumpFilter(int targetAddress)
            {
                return CreateFilter('U', targetAddress - (currentAddress + 3));
            }

            void AddMemoryCell(List<Filter> filters, int length = 1, bool isEnabled = true)
            {
                memoryCells.Add(new MemoryCell { Address = currentAddress, Filters = filters, IsEnabled = isEnabled });
                currentAddress += length;
            }

            int EncodeVolume(double volume)
            {
                return (int)((maxVolume - volume) / (maxVolume - minVolume) * (volumeLevels - 1));
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

                    var filters = noteGroup.Notes
                        .OrderBy(note => (int)note.Instrument * 100 + note.Number)
                        .Select((note, index) => CreateFilter((char)('0' + index), note.Number + ((int)note.Instrument + EncodeVolume(note.Volume) * InstrumentCount) * 256))
                        .Append(CreateFilter('Z', GetHistogram(noteGroup.Notes)))
                        .ToList();

                    var length = (int)(14400 / currentBeatsPerMinute / noteGroup.Length);

                    AddMemoryCell(filters, length: length);
                }

                // Create a disabled jump back to the beginning of the song
                AddMemoryCell(new List<Filter> { CreateJumpFilter(songAddress) }, length: 4, isEnabled: false);

                // Add a gap between songs
                AddMemoryCell(null);
            }

            // Jump back to the beginning
            AddMemoryCell(new List<Filter> { CreateJumpFilter(0) }, length: 4);

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
                .Select(group => Math.Min((int)Math.Ceiling(group.Sum(note => note.Volume)), 3) << (group.Key * 2))
                .Sum();
        }

        private static void WriteOutCommands(string outputCommandsFile, List<Entity> memoryCells, int volumeLevels, double minVolume, double maxVolume)
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
                                var configuration = filter.Count / 256 - 1;
                                var instrument = (Instrument)(configuration % InstrumentCount + 1);
                                var encodedVolume = configuration / InstrumentCount;

                                var note = filter.Count % 256 - 1;
                                var instrumentAndNote = instrument switch
                                {
                                    Instrument.Drumkit => Constants.Drums[note],
                                    _ => $"{instrument} {Notes[note % Notes.Count]}{(note + 5) / Notes.Count + 1}"
                                };
                                var volume = maxVolume - (double)encodedVolume / (volumeLevels - 1) * (maxVolume - minVolume);
                                return $"{instrumentAndNote} at {(int)(volume * 100)}% volume";
                            }
                            else if (signalName == VirtualSignalNames.LetterOrDigit('U'))
                            {
                                return $"jump by {filter.Count} to {address + 1 + filter.Count}";
                            }
                            else if (signalName == VirtualSignalNames.LetterOrDigit('Z'))
                            {
                                return $"histogram {filter.Count:X}";
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
        public int? VolumeLevels { get; set; }
        public double? MinVolume { get; set; }
        public double? MaxVolume { get; set; }
        public string SpreadsheetTabs { get; set; }
    }
}
