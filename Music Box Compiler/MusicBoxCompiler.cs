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
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
            var configFile = configuration.ConfigFile;
            var outputBlueprintFile = configuration.OutputBlueprint;
            var outputJsonFile = configuration.OutputJson;
            var outputConstantsFile = configuration.OutputConstants;
            var outputCommandsFile = configuration.OutputCommands;
            var outputMidiEventsFile = configuration.OutputMidiEvents;
            var baseAddress = configuration.BaseAddress ?? 0;
            var snapToGrid = configuration.SnapToGrid;
            var x = configuration.X;
            var y = configuration.Y;
            var width = configuration.Width ?? 16;
            var height = configuration.Height ?? 16;
            var volumeLevels = configuration.VolumeLevels ?? 10;
            var minVolume = configuration.MinVolume ?? 0.1;
            var maxVolume = configuration.MaxVolume ?? 1;
            var constantsNamespace = configuration.ConstantsNamespace ?? "Music";

            var config = LoadConfig(configFile);

            using var midiEventWriter = outputMidiEventsFile != null ? new StreamWriter(outputMidiEventsFile) : null;

            var playlists = config.Playlists
                .Select(playlistConfig =>
                    new Playlist
                    {
                        Name = playlistConfig.Name,
                        Songs = playlistConfig.Songs
                            .Where(songConfig => !songConfig.Disabled)
                            .SelectMany(songConfig =>
                                Path.GetExtension(songConfig.Source) switch
                                {
                                    ".xlsx" => SpreadsheetReader.ReadSongsFromSpreadsheet(songConfig.Source, new string[] { songConfig.SpreadsheetTab }).Select(song => { song.Name = songConfig.Name; return song; }),
                                    ".mid" => new List<Song> { MidiReader.ReadSong(songConfig.Source, midiEventWriter, songConfig.InstrumentOffsets, ProcessMasterVolume(songConfig.Volume), ProcessInstrumentVolumes(songConfig.InstrumentVolumes), songConfig.Loop, songConfig.Name) },
                                    _ => throw new Exception($"Unsupported source file extension for {songConfig.Source}")
                                }
                            )
                            .ToList(),
                        Loop = playlistConfig.Loop
                    }
                )
                .ToList();

            var blueprint = CreateBlueprintFromPlaylists(playlists, baseAddress, snapToGrid, x, y, width, height, volumeLevels, minVolume, maxVolume, out var addresses);
            BlueprintUtil.PopulateIndices(blueprint);

            var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };
            var memoryCells = blueprint.Entities
                .Where(entity => entity.Name == ItemNames.ConstantCombinator)
                .OrderBy(entity => entity.Position.X - entity.Position.Y * 1000000)
                .ToList();

            BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
            BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
            WriteOutConstants(outputConstantsFile, addresses, constantsNamespace);
            WriteOutCommands(outputCommandsFile, memoryCells, volumeLevels, minVolume, maxVolume);
        }

        private static MusicConfig LoadConfig(string configFile)
        {
            using var reader = new StreamReader(configFile);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            return deserializer.Deserialize<MusicConfig>(reader);
        }

        private static double ProcessMasterVolume(double? masterVolume) => (masterVolume ?? 100) / 100;

        private static Dictionary<Instrument, double> ProcessInstrumentVolumes(Dictionary<Instrument, double> instrumentVolumes) =>
            instrumentVolumes?.Select(entry => (entry.Key, Value: entry.Value / 100))?.ToDictionary(entry => entry.Key, entry => entry.Value);

        private static Blueprint CreateBlueprintFromPlaylists(List<Playlist> playlists, int baseAddress, bool? snapToGrid, int? x, int? y, int width, int height, int volumeLevels, double minVolume, double maxVolume, out Addresses addresses)
        {
            var memoryCells = new List<MemoryCell>();
            var currentAddress = baseAddress;
            var timeDeficit = 0;
            var songContexts = new Dictionary<Song, (Filter jumpFilter, int returnAddress)>();

            addresses = new Addresses();

            Filter CreateJumpFilter(int targetAddress)
            {
                return CreateFilter('U', targetAddress - (currentAddress + 3));
            }

            void AddMemoryCell(List<Filter> filters, int length = 1, bool isEnabled = true)
            {
                memoryCells.Add(new MemoryCell { Address = currentAddress, Filters = filters, IsEnabled = isEnabled });
                currentAddress += length;
            }

            Filter AddJump(int targetAddress, bool isEnabled = true)
            {
                var jumpFilter = CreateJumpFilter(targetAddress);
                AddMemoryCell(new List<Filter> { jumpFilter }, length: 4, isEnabled: isEnabled);
                return jumpFilter;
            }

            int EncodeVolume(double volume)
            {
                return (int)((maxVolume - volume) / (maxVolume - minVolume) * (volumeLevels - 1));
            }

            // Create the jump table
            foreach (var playlist in playlists)
            {
                var playlistAddress = currentAddress;

                if (playlist.Name != null)
                {
                    addresses.PlaylistAddresses[playlist.Name] = playlistAddress;
                }

                foreach (var song in playlist.Songs)
                {
                    if (song.Name != null)
                    {
                        addresses.SongAddresses[song.Name] = currentAddress;
                    }

                    var jumpFilter = AddJump(0);

                    songContexts[song] = (jumpFilter, currentAddress);
                }

                // Create a jump back to the beginning of the playlist
                AddJump(playlistAddress, isEnabled: playlist.Loop);
            }

            // Jump back to the beginning
            AddJump(0);

            // Add the songs
            var trackNumber = 0;
            foreach (var playlist in playlists)
            {
                foreach (var song in playlist.Songs)
                {
                    var songAddress = currentAddress;
                    var (jumpFilter, returnAddress) = songContexts[song];
                    int currentBeatsPerMinute = 60;

                    trackNumber++;

                    // Update the jump table entry to point at the song
                    jumpFilter.Count += songAddress;

                    // Add the notes for the song
                    foreach (var noteGroup in song.NoteGroups)
                    {
                        if (noteGroup.BeatsPerMinute.HasValue)
                        {
                            currentBeatsPerMinute = noteGroup.BeatsPerMinute.Value;
                        }

                        var filters = noteGroup.Notes
                            .OrderBy(note => (int)note.Instrument * 100 + note.Number)
                            .Select((note, index) => CreateFilter((char)('0' + index), note.Number + ((int)note.Instrument + EncodeVolume(note.Volume) * InstrumentCount) * 256))
                            .Append(CreateFilter('X', trackNumber))
                            .Append(CreateFilter('Y', currentAddress - songAddress + 1))
                            .Append(CreateFilter('Z', GetHistogram(noteGroup.Notes)))
                            .ToList();

                        var length = (int)(14400 / currentBeatsPerMinute / noteGroup.Length) - timeDeficit;

                        // We can't have multiple note groups at the same address, so the minimum length must be 1.
                        // However, to avoid delaying future notes we capture the amount that the length is adjusted
                        // as the time deficit and apply that to the next note.
                        if (length < 1)
                        {
                            timeDeficit = 1 - length;
                            length = 1;
                        }
                        else
                        {
                            timeDeficit = 0;
                        }

                        AddMemoryCell(filters, length: length);
                    }

                    // Create a jump back to the beginning of the song
                    AddJump(songAddress, isEnabled: song.Loop);

                    // Add a pause between songs
                    AddMemoryCell(null, length: 120);

                    // Jump to the next song
                    AddJump(returnAddress);
                }
            }

            var romUsed = memoryCells.Count;
            var totalRom = width * height;

            Console.WriteLine($"ROM usage: {romUsed}/{totalRom} ({(double)romUsed / totalRom * 100:F1}%)");

            return RomGenerator.Generate(new RomConfiguration
            {
                SnapToGrid = snapToGrid,
                X = x,
                Y = y,
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

        private static void WriteOutConstants(string outputConstantsFile, Addresses addresses, string constantsNamespace)
        {
            if (outputConstantsFile == null)
            {
                return;
            }

            File.WriteAllText(outputConstantsFile, $@"namespace {constantsNamespace}
{{
    public static class PlaylistAddresses
    {{
{string.Join(Environment.NewLine, addresses.PlaylistAddresses.Select(entry => $"        public const int {entry.Key} = {entry.Value};"))}
    }}

    public static class SongAddresses
    {{
{string.Join(Environment.NewLine, addresses.SongAddresses.Select(entry => $"        public const int {entry.Key} = {entry.Value};"))}
    }}
}}
");
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
                                //return $"jump by {filter.Count} to {address + 1 + filter.Count}";
                                return $"jump by {filter.Count}";
                            }
                            else if (signalName == VirtualSignalNames.LetterOrDigit('X'))
                            {
                                return $"track {filter.Count}";
                            }
                            else if (signalName == VirtualSignalNames.LetterOrDigit('Y'))
                            {
                                return $"offset {filter.Count}";
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

        private class Addresses
        {
            public Dictionary<string, int> PlaylistAddresses { get; } = new Dictionary<string, int>();
            public Dictionary<string, int> SongAddresses { get; } = new Dictionary<string, int>();
        }
    }

    public class MusicBoxConfiguration
    {
        public string ConfigFile { get; set; }
        public string OutputBlueprint { get; set; }
        public string OutputJson { get; set; }
        public string OutputConstants { get; set; }
        public string OutputCommands { get; set; }
        public string OutputMidiEvents { get; set; }
        public int? BaseAddress { get; set; }
        public bool? SnapToGrid { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? VolumeLevels { get; set; }
        public double? MinVolume { get; set; }
        public double? MaxVolume { get; set; }
        public string ConstantsNamespace { get; set; }
    }
}
