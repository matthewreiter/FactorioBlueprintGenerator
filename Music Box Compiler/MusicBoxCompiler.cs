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
        private static readonly Regex NoteGroupSignalRegex = new Regex(@"^signal-([A-Q])$");

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
            var baseAddress = configuration.BaseAddress ?? 1;
            var snapToGrid = configuration.SnapToGrid;
            var x = configuration.X;
            var y = configuration.Y;
            var width = configuration.Width ?? 16;
            var height = configuration.Height ?? 16;
            var volumeLevels = configuration.VolumeLevels ?? 10;
            var minVolume = configuration.MinVolume ?? 0.1;
            var maxVolume = configuration.MaxVolume ?? 1;
            var baseMetadataAddress = configuration.BaseMetadataAddress ?? 1;
            var constantsNamespace = configuration.ConstantsNamespace ?? "Music";

            var config = LoadConfig(configFile);

            var playlists = config.Playlists
                .AsParallel()
                .Select(playlistConfig =>
                    new Playlist
                    {
                        Name = playlistConfig.Name,
                        Songs = playlistConfig.Songs
                            .AsParallel()
                            .Where(songConfig => !songConfig.Disabled)
                            .Select(songConfig =>
                                Path.GetExtension(songConfig.Source).ToLower() switch
                                {
                                    ".xlsx" => SpreadsheetReader.ReadSongFromSpreadsheet(songConfig.Source, songConfig.SpreadsheetTab),
                                    ".mid" => MidiReader.ReadSong(songConfig.Source, outputMidiEventsFile != null, songConfig.InstrumentOffsets, ProcessMasterVolume(songConfig.Volume), ProcessInstrumentVolumes(songConfig.InstrumentVolumes)),
                                    _ => throw new Exception($"Unsupported source file extension for {songConfig.Source}")
                                } with { Name = songConfig.Name, DisplayName = songConfig.DisplayName, Loop = songConfig.Loop, AddressIndex = songConfig.AddressIndex }
                            )
                            .ToList(),
                        Loop = playlistConfig.Loop
                    }
                )
                .ToList();

            var blueprint = CreateBlueprintFromPlaylists(playlists, baseAddress, snapToGrid, x, y, width, height, volumeLevels, minVolume, maxVolume, baseMetadataAddress, out var addresses);
            BlueprintUtil.PopulateIndices(blueprint);

            var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };
            var memoryCells = blueprint.Entities
                .Where(entity => entity.Name == ItemNames.ConstantCombinator)
                .OrderBy(entity => entity.Position.X - entity.Position.Y * 1000000)
                .ToList();

            BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
            BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
            WriteOutConstants(outputConstantsFile, addresses, constantsNamespace);
            WriteOutMidiEvents(outputMidiEventsFile, playlists);
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

        private static Blueprint CreateBlueprintFromPlaylists(List<Playlist> playlists, int baseAddress, bool? snapToGrid, int? x, int? y, int width, int height, int volumeLevels, double minVolume, double maxVolume, int baseMetadataAddress, out Addresses addresses)
        {
            const int noteGroupAddressBits = 16;
            const int noteGroupTimeOffsetBits = 12;

            var memoryCells = new List<MemoryCell>();
            var noteGroupCells = new List<MemoryCell>();
            var constantCells = new List<MemoryCell>();
            var allNoteTuples = new HashSet<NoteTuple>();
            var noteTuplesToAddresses = new Dictionary<NoteTuple, (int Address, int SubAddress)>();
            var currentAddress = baseAddress;
            var initialNoteAddress = 1 << noteGroupAddressBits;

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

            NoteTuple CreateNoteTuple(NoteGroup noteGroup)
            {
                var notes = noteGroup.Notes
                    .OrderBy(note => (int)note.Instrument * 100 + note.Number)
                    .Select(note => note.Number + ((int)note.Instrument + EncodeVolume(note.Volume) * InstrumentCount) * 256);

                return new(
                    notes.ElementAtOrDefault(0),
                    notes.ElementAtOrDefault(1),
                    notes.ElementAtOrDefault(2),
                    notes.ElementAtOrDefault(3),
                    notes.ElementAtOrDefault(4),
                    notes.ElementAtOrDefault(5),
                    notes.ElementAtOrDefault(6),
                    notes.ElementAtOrDefault(7),
                    notes.ElementAtOrDefault(8),
                    notes.ElementAtOrDefault(9)
                );
            }

            var allSongs = playlists.SelectMany(playlist => playlist.Songs).ToList();

            var currentMetadataAddressIndex = 0;
            var reservedMetadataAddressIndices = allSongs.Where(song => song.AddressIndex != null).Select(song => song.AddressIndex.Value).ToHashSet();

            int AllocateNextMetadataAddress()
            {
                while (reservedMetadataAddressIndices.Contains(currentMetadataAddressIndex))
                {
                    currentMetadataAddressIndex++;
                }

                return currentMetadataAddressIndex++;
            }

            AddJump(initialNoteAddress);
            var currentNoteGroupAddress = currentAddress;
            currentAddress = initialNoteAddress;

            // Add note groups
            var noteTuplesWithHistograms = allSongs
                .SelectMany(song => song.NoteGroups)
                .OrderByDescending(noteGroup => noteGroup.Notes.Count)
                .Select(noteGroup => new NoteTupleWithHistogram(CreateNoteTuple(noteGroup), GetHistogram(noteGroup.Notes)))
                .ToList();

            var noteGroupCellDataByFreeSpace = Enumerable.Range(0, 18).Select(index => new Queue<List<NoteTupleWithHistogram>>()).ToList();

            foreach (var noteTupleWithHistogram in noteTuplesWithHistograms)
            {
                if (!allNoteTuples.Add(noteTupleWithHistogram.NoteTuple))
                {
                    continue;
                }

                var spaceRequired = noteTupleWithHistogram.NoteTuple.Count() + 1;

                for (int freeSpace = spaceRequired; ; freeSpace++)
                {
                    List<NoteTupleWithHistogram> noteGroupCellData = null;
                    if (freeSpace >= noteGroupCellDataByFreeSpace.Count || noteGroupCellDataByFreeSpace[freeSpace].TryDequeue(out noteGroupCellData))
                    {
                        if (noteGroupCellData == null)
                        {
                            noteGroupCellData = new List<NoteTupleWithHistogram>();
                        }

                        noteGroupCellData.Add(noteTupleWithHistogram);

                        var newFreeSpace = noteGroupCellData.Count < DecoderConstants.AllNoteGroupSignals.Count ? freeSpace - spaceRequired : 0;
                        noteGroupCellDataByFreeSpace[newFreeSpace].Enqueue(noteGroupCellData);

                        break;
                    }
                }
            }

            foreach (var noteGroupCellData in noteGroupCellDataByFreeSpace.SelectMany(list => list))
            {
                var noteGroupAddress = currentNoteGroupAddress++;

                var noteGroupCellIntermediateData = noteGroupCellData.Select((noteTupleWithHistogram, index) =>
                {
                    var noteTuple = noteTupleWithHistogram.NoteTuple;
                    var noteGroupSignals = DecoderConstants.AllNoteGroupSignals[index];

                    var noteGroupFilters = noteTuple.Select((note, index) => CreateItemFilter(noteGroupSignals.NoteSignals[index], note))
                        .Append(CreateItemFilter(noteGroupSignals.HistogramSignal, noteTupleWithHistogram.Histogram))
                        .ToList();

                    return (Tuple: noteTuple, Filters: noteGroupFilters, SubAddress: index);
                }).ToList();

                foreach (var (noteTuple, noteGroupFilters, subAddress) in noteGroupCellIntermediateData)
                {
                    noteTuplesToAddresses[noteTuple] = (noteGroupAddress, subAddress);
                }

                var filters = noteGroupCellIntermediateData.SelectMany(data => data.Filters).ToList();

                noteGroupCells.Add(new MemoryCell { Address = noteGroupAddress, Filters = filters, IsEnabled = true });
            }

            // Add the songs
            var trackNumber = 0;
            foreach (var playlist in playlists)
            {
                var playlistAddress = currentAddress;

                foreach (var song in playlist.Songs)
                {
                    var metadataAddress = baseMetadataAddress + (song.AddressIndex ?? AllocateNextMetadataAddress());
                    var songAddress = currentAddress;
                    var currentFilters = new List<Filter>();
                    var currentBeatsPerMinute = 60;
                    var currentTimeOffset = 0;
                    var cellStartTime = 0;
                    var timeDeficit = 0;

                    trackNumber++;

                    // Add the notes for the song
                    foreach (var noteGroup in song.NoteGroups)
                    {
                        if (noteGroup.BeatsPerMinute.HasValue)
                        {
                            currentBeatsPerMinute = noteGroup.BeatsPerMinute.Value;
                        }

                        var (noteGroupAddress, noteGroupSubAddress) = noteTuplesToAddresses[CreateNoteTuple(noteGroup)];

                        if (currentFilters.Count == 0)
                        {
                            currentFilters.Add(CreateFilter('Y', metadataAddress + (currentTimeOffset + 1) * 256));
                        }

                        var length = (int)(14400 / currentBeatsPerMinute / noteGroup.Length) - timeDeficit;

                        // We can't have multiple note groups play too close too each other.
                        // However, to avoid delaying future notes we capture the amount that the length is adjusted
                        // as the time deficit and apply that to the next note.
                        const int minimumLength = 1;
                        if (length < minimumLength)
                        {
                            timeDeficit = minimumLength - length;
                            length = minimumLength;
                        }
                        else
                        {
                            timeDeficit = 0;
                        }

                        var noteGroupTimeOffset = Math.Min(currentTimeOffset - cellStartTime - currentFilters.Count + 2, (1 << noteGroupTimeOffsetBits) - 1);
                        currentFilters.Add(CreateFilter((char)('A' + currentFilters.Count - 1), noteGroupAddress + (noteGroupTimeOffset << noteGroupAddressBits) + (noteGroupSubAddress << noteGroupAddressBits + noteGroupTimeOffsetBits)));

                        currentTimeOffset += length;

                        if (currentFilters.Count >= 18)
                        {
                            const int minimumCellLength = 19; // The number of cycles required to finish loading all of the note groups
                            var cellLength = currentTimeOffset - cellStartTime;

                            if (cellLength < currentFilters.Count)
                            {
                                timeDeficit += minimumCellLength - cellLength;
                                cellLength = minimumCellLength;
                            }

                            AddMemoryCell(currentFilters, cellLength);

                            currentFilters = new List<Filter>();
                            cellStartTime = currentTimeOffset;
                        }
                    }

                    if (currentFilters.Count > 0)
                    {
                        var cellLength = Math.Max(currentTimeOffset - cellStartTime, currentFilters.Count + 1);

                        AddMemoryCell(currentFilters, cellLength);
                    }

                    var songLength = currentAddress - songAddress;

                    // Create a jump back to the beginning of the song
                    AddJump(songAddress, isEnabled: song.Loop);

                    // Add a pause between songs
                    currentAddress += 120;

                    // Add song metadata
                    if (song.Name != null)
                    {
                        addresses.SongMetadataAddresses[song.Name] = metadataAddress;
                    }

                    var metadataFilters = new List<Filter>
                    {
                        CreateFilter('0', songAddress),
                        CreateFilter('1', trackNumber),
                        CreateFilter('2', songLength)
                    };

                    var displayName = song.DisplayName ?? song.Name;
                    if (displayName != null)
                    {
                        const int maxCharactersToDisplay = 24;
                        var charactersToDisplay = Math.Min(displayName.Length, maxCharactersToDisplay);
                        var encodedBlock = 0;
                        var blockIndex = 0;

                        for (var index = 0; index < charactersToDisplay; index++)
                        {
                            var currentCharacter = (byte)displayName[index];
                            var positionInBlock = index % 4;

                            encodedBlock |= currentCharacter << (positionInBlock * 8);

                            if (positionInBlock == 3 || index == charactersToDisplay - 1)
                            {
                                metadataFilters.Add(CreateFilter((char)('A' + blockIndex), encodedBlock));
                                encodedBlock = 0;
                                blockIndex++;
                            }
                        }
                    }

                    constantCells.Add(new MemoryCell { Address = metadataAddress, Filters = metadataFilters });
                }

                // Create a jump back to the beginning of the playlist
                AddJump(playlistAddress, isEnabled: playlist.Loop);
            }

            // Jump back to the beginning
            AddJump(0);

            memoryCells.AddRange(noteGroupCells);

            var romUsed = memoryCells.Count + constantCells.Count;
            var totalRom = width * height;

            Console.WriteLine($"ROM usage: {romUsed}/{totalRom} ({(double)romUsed / totalRom * 100:F1}%)");

            return RomGenerator.Generate(new RomConfiguration
            {
                SnapToGrid = snapToGrid,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                ProgramRows = height - 1, // Allocate one line for the constant cells
                ProgramName = "Songs",
                IconItemNames = new List<string> { ItemNames.ElectronicCircuit, ItemNames.ProgrammableSpeaker }
            }, memoryCells, constantCells);
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

            File.WriteAllText(outputConstantsFile, $@"using SeeSharp.Runtime;

namespace {constantsNamespace}
{{
    public static class PlaylistAddresses
    {{
        {string.Join($"{Environment.NewLine}        ", addresses.PlaylistAddresses.Select(entry => $"public const int {entry.Key} = {entry.Value};"))}
    }}

    public static class SongMetadataAddresses
    {{
        {string.Join($"{Environment.NewLine}        ", addresses.SongMetadataAddresses.Select(entry => $"public const int {entry.Key} = {entry.Value};"))}
    }}
}}
");
        }

        private static void WriteOutMidiEvents(string outputMidiEventsFile, List<Playlist> playlists)
        {
            if (outputMidiEventsFile != null)
            {
                using var midiEventStream = File.Create(outputMidiEventsFile);

                foreach (var playlist in playlists)
                {
                    foreach (var song in playlist.Songs)
                    {
                        var debugStream = song.DebugStream;

                        if (debugStream != null)
                        {
                            debugStream.Position = 0;
                            debugStream.CopyTo(midiEventStream);
                        }
                    }
                }
            }
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
                                    Instrument.Drumkit => Constants.Drums.ElementAtOrDefault(note) ?? note.ToString(),
                                    _ => $"{instrument} {Notes.ElementAtOrDefault(note % Notes.Count) ?? (note % Notes.Count).ToString()}{(note + 5) / Notes.Count + 1}"
                                };
                                var volume = maxVolume - (double)encodedVolume / (volumeLevels - 1) * (maxVolume - minVolume);
                                return $"{instrumentAndNote} at {(int)(volume * 100)}% volume";
                            }
                            else if ((match = NoteGroupSignalRegex.Match(signalName)).Success)
                            {
                                const int maxAddresses = 1 << 20;
                                var relativeNoteGroupAddress = filter.Count % maxAddresses;
                                var noteGroupTimeOffset = filter.Count / maxAddresses;

                                return $"note group {relativeNoteGroupAddress} with time offset {noteGroupTimeOffset}";
                            }
                            else if (signalName == VirtualSignalNames.LetterOrDigit('U'))
                            {
                                //return $"jump by {filter.Count} to {address + 1 + filter.Count}";
                                return $"jump by {filter.Count}";
                            }
                            else if (signalName == VirtualSignalNames.LetterOrDigit('Y'))
                            {
                                return $"offset {filter.Count % 65536}, track {filter.Count / 65536}";
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

        private static Filter CreateItemFilter(string name, int count)
        {
            return new Filter { Signal = new SignalID { Name = name, Type = SignalTypes.Item }, Count = count };
        }

        private class Addresses
        {
            public Dictionary<string, int> PlaylistAddresses { get; } = new Dictionary<string, int>();
            public Dictionary<string, int> SongMetadataAddresses { get; } = new Dictionary<string, int>();
        }

        private record NoteTuple(int Note0, int Note1, int Note2, int Note3, int Note4, int Note5, int Note6, int Note7, int Note8, int Note9) : IEnumerable<int>
        {
            public IEnumerator<int> GetEnumerator()
            {
                if (Note0 != 0) yield return Note0;
                if (Note1 != 0) yield return Note1;
                if (Note2 != 0) yield return Note2;
                if (Note3 != 0) yield return Note3;
                if (Note4 != 0) yield return Note4;
                if (Note5 != 0) yield return Note5;
                if (Note6 != 0) yield return Note6;
                if (Note7 != 0) yield return Note7;
                if (Note8 != 0) yield return Note8;
                if (Note9 != 0) yield return Note9;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private record NoteTupleWithHistogram(NoteTuple NoteTuple, int Histogram);
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
        public int? BaseMetadataAddress { get; set; }
        public string ConstantsNamespace { get; set; }
    }
}
