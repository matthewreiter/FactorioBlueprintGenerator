using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator;
using BlueprintGenerator.Constants;
using Microsoft.Extensions.Configuration;
using MusicBoxCompiler.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MusicBoxCompiler;

public static class MusicBoxCompiler
{
    private const int InstrumentCount = 12;
    private const int InstrumentCountV2 = 10;
    private const int PitchCountV2 = 48;
    private const int VolumeCountV2 = 100;
    private const int ChannelCount = 10;
    private const int ChannelCountV2 = 48;
    private const int NoteGroupAddressBits = 16;
    private const int NoteGroupTimeOffsetBits = 11;
    private const int MetadataAddressBits = 10;
    private const int MinimumNoteDuration = 10;
    private const int ChannelCooldownTicks = 1;

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
        var outputMidiEventsFile = configuration.OutputMidiEvents;
        var version = configuration.Version ?? 1;
        var baseMetadataAddress = configuration.BaseMetadataAddress ?? 1;
        var constantsNamespace = configuration.ConstantsNamespace ?? "Music";

        var config = LoadConfig(configFile);

        var playlists = config.Playlists
            .AsParallel()
            .Where(playlistConfig => !playlistConfig.Disabled)
            .Select(playlistConfig =>
                new Playlist
                {
                    Name = playlistConfig.Name,
                    Songs = playlistConfig.Songs
                        .SelectMany(songConfig => Path.GetExtension(songConfig.Source).ToLower() switch
                        {
                            ".yaml" => LoadConfig(songConfig.Source).Playlists.Find(playlist => playlist.Name == songConfig.SourcePlaylist)?.Songs ?? [],
                            _ => [songConfig]
                        })
                        .SelectMany(songConfig =>
                        {
                            if (songConfig.Source.Contains('*'))
                            {
                                var directoryName = Path.GetDirectoryName(songConfig.Source);
                                var fileName = Path.GetFileName(songConfig.Source);
                                var files = Directory.GetFiles(directoryName, fileName);

                                if (files.Length > 1)
                                {
                                    return files.OrderBy(file => file)
                                        .Select((source, index) => songConfig with
                                        {
                                            Name = $"{songConfig.Name}Part{index + 1}",
                                            DisplayName = songConfig.DisplayName != null ? $"{songConfig.DisplayName} (Part {index + 1})" : null,
                                            Source = source,
                                            Gapless = songConfig.Gapless
                                        });
                                }
                            }

                            return [songConfig];
                        })
                        .ToList() // Store the intermediate results as a list to preserve the order when parallelized
                        .AsParallel()
                        .Where(songConfig => !songConfig.Disabled)
                        .Select(songConfig =>
                            Path.GetExtension(songConfig.Source).ToLower() switch
                            {
                                ".xlsx" => SpreadsheetReader.ReadSongFromSpreadsheet(songConfig.Source, songConfig.SpreadsheetTab),
                                ".mid" => MidiReader.ReadSong(
                                    songConfig.Source,
                                    outputMidiEventsFile != null,
                                    songConfig.InstrumentOffsets,
                                    ProcessMasterVolume(songConfig.Volume),
                                    ProcessInstrumentVolumes(songConfig.InstrumentVolumes),
                                    !songConfig.SuppressInstrumentFallback,
                                    songConfig.ExpandNotes && version == 1,
                                    version == 1 ? ChannelCount : null),
                                _ => throw new Exception($"Unsupported source file extension for {songConfig.Source}")
                            } with { Name = songConfig.Name, DisplayName = songConfig.DisplayName, Artist = songConfig.Artist, Loop = songConfig.Loop, Gapless = songConfig.Gapless, AddressIndex = songConfig.AddressIndex }
                        )
                        .ToList(),
                    Loop = playlistConfig.Loop
                }
            )
            .ToList();

        if (config.IncludeBlankSong)
        {
            playlists.Add(new()
            {
                Songs =
                [
                    new()
                    {
                        Name = "Blank",
                        DisplayName = "",
                        AddressIndex = 1000 - baseMetadataAddress,
                        NoteGroups = [new() { Length = TimeSpan.FromSeconds(10), Notes = [] }],
                    }
                ]
            });
        }

        Addresses addresses;
        var blueprint = version switch
        {
            1 => CreateBlueprintFromPlaylists(playlists, configuration, out addresses),
            2 => CreateBlueprintFromPlaylistsV2(playlists, configuration, out addresses),
            _ => throw new Exception($"Unsupported version: {version}"),
        };
        BlueprintUtil.PopulateIndices(blueprint);

        var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };

        BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
        BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
        WriteOutConstants(outputConstantsFile, addresses, constantsNamespace);
        WriteOutMidiEvents(outputMidiEventsFile, playlists);
    }

    private static MusicConfig LoadConfig(string configFile)
    {
        var basePath = Path.GetDirectoryName(configFile);
        using var reader = new StreamReader(configFile);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var config = deserializer.Deserialize<MusicConfig>(reader);

        return config with
        {
            Playlists = [.. config.Playlists.Select(playlistConfig => playlistConfig with
            {
                Songs = [.. playlistConfig.Songs.Select(songConfig => songConfig with
                {
                    Source = Path.Combine(basePath, songConfig.Source)
                })]
            })]
        };
    }

    private static double ProcessMasterVolume(double? masterVolume) => (masterVolume ?? 100) / 100;

    private static Dictionary<Instrument, double> ProcessInstrumentVolumes(Dictionary<Instrument, double> instrumentVolumes) =>
        instrumentVolumes?.Select(entry => (entry.Key, Value: entry.Value / 100))?.ToDictionary(entry => entry.Key, entry => entry.Value);

    private static Blueprint CreateBlueprintFromPlaylists(List<Playlist> playlists, MusicBoxConfiguration configuration, out Addresses addresses)
    {
        var baseAddress = configuration.BaseAddress ?? 1;
        var baseNoteAddress = configuration.BaseNoteAddress ?? 1 << NoteGroupAddressBits;
        var baseMetadataAddress = configuration.BaseMetadataAddress ?? 1;
        var nextAddress = configuration.NextAddress ?? baseAddress;
        var snapToGrid = configuration.SnapToGrid;
        var x = configuration.X;
        var y = configuration.Y;
        var width = configuration.Width ?? 16;
        var height = configuration.Height ?? 16;
        var volumeLevels = configuration.VolumeLevels ?? 10;
        var minVolume = configuration.MinVolume ?? 0.1;
        var maxVolume = configuration.MaxVolume ?? 1;

        var songCells = new List<MemoryCell>();
        var noteGroupCells = new List<MemoryCell>();
        var metadataCells = new List<MemoryCell>();
        var allNoteTuples = new HashSet<NoteTuple>();
        var noteTuplesToAddresses = new Dictionary<NoteTuple, (int Address, int SubAddress)>();
        var currentAddress = baseAddress;
        var maxFilters = 40;
        var totalPlayTime = 0;

        addresses = new Addresses();

        Filter CreateJumpFilter(int targetAddress)
        {
            return CreateFilter('U', targetAddress - (currentAddress + 3));
        }

        void AddMemoryCell(List<Filter> filters, int length = 1, bool isEnabled = true)
        {
            songCells.Add(new() { Address = currentAddress, Filters = filters, IsEnabled = isEnabled });
            currentAddress += length;
        }

        Filter AddJump(int targetAddress, bool isEnabled = true)
        {
            var jumpFilter = CreateJumpFilter(targetAddress);
            AddMemoryCell([jumpFilter], length: 4, isEnabled: isEnabled);
            return jumpFilter;
        }

        int EncodeVolume(double volume)
        {
            return (int)((maxVolume - volume) / (maxVolume - minVolume) * (volumeLevels - 1));
        }

        NoteTuple CreateNoteTuple(NoteGroup noteGroup)
        {
            var notes = noteGroup.Notes
                .OrderBy(note => note.Instrument).ThenBy(note => note.Number)
                .Select(note => note.Number + ((int)note.Instrument + EncodeVolume(note.Volume) * InstrumentCount) * 256)
                .ToArray();

            return new(notes);
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

        AddJump(baseNoteAddress);
        var currentNoteGroupAddress = currentAddress;
        currentAddress = baseNoteAddress;

        // Add note groups
        var noteTuples = allSongs
            .SelectMany(song => song.NoteGroups)
            .OrderByDescending(noteGroup => noteGroup.Notes.Count)
            .Select(CreateNoteTuple)
            .ToList();

        var noteGroupCellDataByFreeSpace = Enumerable.Range(0, maxFilters).Select(index => new Queue<List<NoteTuple>>()).ToList();

        foreach (var noteTuple in noteTuples)
        {
            if (!allNoteTuples.Add(noteTuple))
            {
                continue;
            }

            var spaceRequired = noteTuple.Count();

            for (int freeSpace = spaceRequired; ; freeSpace++)
            {
                List<NoteTuple> noteGroupCellData = null;
                if (freeSpace >= noteGroupCellDataByFreeSpace.Count || noteGroupCellDataByFreeSpace[freeSpace].TryDequeue(out noteGroupCellData))
                {
                    (noteGroupCellData ??= []).Add(noteTuple);

                    var newFreeSpace = noteGroupCellData.Count < DecoderConstants.AllNoteGroupSignals.Count ? freeSpace - spaceRequired : 0;
                    noteGroupCellDataByFreeSpace[newFreeSpace].Enqueue(noteGroupCellData);

                    break;
                }
            }
        }

        foreach (var noteGroupCellData in noteGroupCellDataByFreeSpace.SelectMany(list => list))
        {
            var noteGroupAddress = currentNoteGroupAddress++;

            var noteGroupCellIntermediateData = noteGroupCellData.Select((noteTuple, index) =>
            {
                var noteGroupSignals = DecoderConstants.AllNoteGroupSignals[index];
                var noteGroupFilters = noteTuple.Select((note, index) => CreateFilter(noteGroupSignals.NoteSignals[index], note)).ToList();

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

            if (playlist.Name is not null)
            {
                addresses.PlaylistAddresses[playlist.Name] = playlistAddress;
            }

            foreach (var song in playlist.Songs)
            {
                var metadataAddress = baseMetadataAddress + (song.AddressIndex ?? AllocateNextMetadataAddress());
                var songAddress = currentAddress;
                var currentFilters = new List<Filter>();
                var currentTimeOffset = 0;
                var cellStartTime = 0;
                var timeDeficit = 0;

                trackNumber++;

                // Add the notes for the song
                foreach (var noteGroup in song.NoteGroups)
                {
                    // Strip leading silence
                    if (song.Gapless && noteGroup.Notes.Count == 0)
                    {
                        continue;
                    }

                    var (noteGroupAddress, noteGroupSubAddress) = noteTuplesToAddresses[CreateNoteTuple(noteGroup)];

                    if (currentFilters.Count == 0)
                    {
                        currentFilters.Add(CreateFilter('Y', metadataAddress + ((currentTimeOffset + 1) << MetadataAddressBits)));
                    }

                    // Strip trailing silence
                    var noteGroupLength = noteGroup.Length;
                    if (song.Gapless && noteGroup == song.NoteGroups[^1] && song.NoteGroups.Count > 1)
                    {
                        var previousNoteGroupLength = song.NoteGroups[^2].Length;
                        if (noteGroupLength > previousNoteGroupLength)
                        {
                            noteGroupLength = previousNoteGroupLength;
                        }
                    }

                    var length = (int)(noteGroupLength.TotalSeconds * 60) - timeDeficit;

                    // We can't have multiple note groups play too close to each other.
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

                    var signal = DecoderConstants.NoteGroupReferenceSignals[currentFilters.Count - 1];
                    var noteGroupTimeOffset = Math.Min(currentTimeOffset - cellStartTime - currentFilters.Count + 2, (1 << NoteGroupTimeOffsetBits) - 1);
                    currentFilters.Add(CreateFilter(signal, noteGroupAddress + (noteGroupTimeOffset << NoteGroupAddressBits) + (noteGroupSubAddress << (NoteGroupAddressBits + NoteGroupTimeOffsetBits))));

                    currentTimeOffset += length;

                    if (currentFilters.Count >= maxFilters)
                    {
                        var minimumCellLength = maxFilters + 1; // The number of cycles required to finish loading all of the note groups
                        var cellLength = currentTimeOffset - cellStartTime;

                        if (cellLength < currentFilters.Count)
                        {
                            timeDeficit += minimumCellLength - cellLength;
                            cellLength = minimumCellLength;
                        }

                        AddMemoryCell(currentFilters, cellLength);

                        currentFilters = [];
                        cellStartTime = currentTimeOffset;
                    }
                }

                if (currentFilters.Count > 0)
                {
                    var cellLength = Math.Max(currentTimeOffset - cellStartTime, currentFilters.Count + 1);

                    AddMemoryCell(currentFilters, cellLength);
                }

                var songLength = currentAddress - songAddress;
                totalPlayTime += songLength;

                // Create a jump back to the beginning of the song
                AddJump(songAddress, isEnabled: song.Loop);

                // Add a pause between songs
                if (!song.Gapless)
                {
                    currentAddress += 120;
                }

                // Add song metadata
                if (song.Name != null)
                {
                    addresses.SongMetadataAddresses[song.Name] = metadataAddress;
                }

                var metadataFilters = new List<Filter>
                {
                    CreateFilter('0', songAddress),
                    CreateFilter('1', trackNumber),
                    CreateFilter('2', (songLength + 59) / 60 * 60) // Round up to the next second
                };

                var displayName = song.DisplayName ?? song.Name;
                if (displayName != null)
                {
                    metadataFilters.AddRange(CreateFiltersForString(displayName, 32, 'A'));
                }

                if (song.Artist != null)
                {
                    metadataFilters.AddRange(CreateFiltersForString(song.Artist, 20, 'I'));
                }

                metadataCells.Add(new MemoryCell { Address = metadataAddress, Filters = metadataFilters });
            }

            // Create a jump back to the beginning of the playlist
            AddJump(playlistAddress, isEnabled: playlist.Loop);
        }

        AddJump(nextAddress);

        songCells.AddRange(noteGroupCells);

        var romUsed = songCells.Count;
        var totalRom = width * (height - 1);

        Console.WriteLine($"Total play time: {TimeSpan.FromSeconds(totalPlayTime / 60d):h\\:mm\\:ss\\.fff}");
        Console.WriteLine($"ROM usage: {romUsed}/{totalRom} ({(double)romUsed / totalRom * 100:F1}%)");

        return RomGenerator.Generate(new RomConfiguration
        {
            SnapToGrid = snapToGrid,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            ProgramRows = 1, // Allocate one line for the constant cells
            ProgramName = "Songs",
            IconNames = [ItemNames.ElectronicCircuit, ItemNames.ProgrammableSpeaker]
        }, metadataCells, songCells);
    }

    private static Blueprint CreateBlueprintFromPlaylistsV2(List<Playlist> playlists, MusicBoxConfiguration configuration, out Addresses addresses)
    {
        var baseAddress = configuration.BaseAddress ?? 1;
        var baseMetadataAddress = configuration.BaseMetadataAddress ?? 1;
        var nextAddress = configuration.NextAddress ?? baseAddress;
        var snapToGrid = configuration.SnapToGrid;
        var x = configuration.X;
        var y = configuration.Y;
        var width = configuration.Width ?? 16;
        var height = configuration.Height ?? 16;

        var songCells = new List<MemoryCell>();
        var metadataCells = new List<MemoryCell>();
        var songDataToCells = new Dictionary<MemoryCellData, MemoryCell>();
        var channelRemainingTimes = new int[ChannelCountV2];
        var currentAddress = baseAddress;
        var totalPlayTime = 0;

        addresses = new Addresses();

        Filter AddJump(int targetAddress, bool isEnabled = true)
        {
            var jumpFilter = CreateFilter('U', targetAddress - (currentAddress + 3));
            songCells.Add(new() { Address = currentAddress, Filters = [jumpFilter], IsEnabled = isEnabled });
            currentAddress += 4;
            return jumpFilter;
        }

        int EncodeDuration(TimeSpan duration) => Math.Max((int)double.Ceiling(duration.TotalSeconds * 60), MinimumNoteDuration);

        int EncodeVolume(double volume) => Math.Min(Math.Max((int)double.Round(volume * 100), 1), 100) - 1;

        int EncodeVolumeChange(Note note) => EncodeVolume(note.Volume) - EncodeVolume(note.PreviousNote.Volume);

        int EncodeNote(Note note)
        {
            var encodedDuration = EncodeDuration(note.Duration);
            var encodedInstrument = (int)note.Instrument - 3;
            var encodedPitch = note.Number - 1;
            var encodedVolume = EncodeVolume(note.Volume);

            return encodedVolume + (encodedPitch + (encodedInstrument + encodedDuration * InstrumentCountV2) * PitchCountV2) * VolumeCountV2;
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

        // Add the songs
        var trackNumber = 0;
        foreach (var playlist in playlists)
        {
            var playlistAddress = currentAddress;

            if (playlist.Name is not null)
            {
                addresses.PlaylistAddresses[playlist.Name] = playlistAddress;
            }

            foreach (var song in playlist.Songs)
            {
                var metadataAddress = baseMetadataAddress + (song.AddressIndex ?? AllocateNextMetadataAddress());
                var songAddress = currentAddress;
                List<(int Address, List<(int Channel, int Note)> Notes)> noteGroupGroups = [];
                List<List<(int Address, List<(int Channel, int Note)> Notes)>> noteGroupGroupGroups = [];
                var timeDeficit = 0;

                trackNumber++;

                // Add the notes for the song
                foreach (var noteGroup in song.NoteGroups)
                {
                    // Strip leading silence
                    if (song.Gapless && noteGroup.Notes.Count == 0)
                    {
                        continue;
                    }

                    var channelNotes = new int[ChannelCountV2];

                    foreach (var note in noteGroup.Notes.OrderBy(note => note.Instrument).ThenBy(note => note.Number))
                    {
                        var channelIndex = -1;

                        if (note.PreviousNote is not null)
                        {
                            // Use the same channel as the previous note
                            var previousChannel = note.PreviousNote.Channel;
                            if (!previousChannel.HasValue)
                            {
                                continue;
                            }

                            channelIndex = previousChannel.Value;

                            // Add the note to the channel
                            note.Channel = channelIndex;
                            channelNotes[channelIndex] = EncodeVolumeChange(note);
                        }
                        else
                        {
                            // Find the best available channel by attempting to spread the notes out by pitch
                            var desiredChannel = (note.Number - 1) * ChannelCountV2 / 48;

                            for (var index = 0; index < ChannelCountV2; index++)
                            {
                                var offsetIndex = (desiredChannel + index) % ChannelCountV2;

                                if (channelRemainingTimes[offsetIndex] == 0)
                                {
                                    channelIndex = offsetIndex;
                                    break;
                                }
                            }

                            // If all channels are busy, give up
                            if (channelIndex == -1)
                            {
                                continue;
                            }

                            // Allocate time on the channel
                            var noteDuration = EncodeDuration(note.Duration);
                            channelRemainingTimes[channelIndex] = noteDuration + ChannelCooldownTicks;

                            // Add the note to the channel
                            note.Channel = channelIndex;
                            channelNotes[channelIndex] = EncodeNote(note);
                        }
                    }

                    var noteTuple = new NoteTuple(channelNotes);

                    var notes = channelNotes
                        .Select((note, channelIndex) => (Channel: channelIndex, Note: note))
                        .Where(note => note.Note != 0)
                        .ToList();

                    if (notes.Count > 0)
                    {
                        noteGroupGroups.Add((currentAddress, notes));

                        if (noteGroupGroups.Count == SpeakerChannelSignals.AllSignalGroups.Count)
                        {
                            noteGroupGroupGroups.Add(noteGroupGroups);
                            noteGroupGroups = [];
                        }
                    }

                    var length = (int)(noteGroup.Length.TotalSeconds * 60) - timeDeficit;

                    // We can't have multiple note groups play too close to each other.
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

                    currentAddress += length;

                    // Reduce channel remaining times
                    for (var channel = 0; channel < ChannelCountV2; channel++)
                    {
                        channelRemainingTimes[channel] = Math.Max(0, channelRemainingTimes[channel] - length);
                    }
                }

                if (noteGroupGroups.Count > 0)
                {
                    noteGroupGroupGroups.Add(noteGroupGroups);
                }

                var songLength = currentAddress - songAddress;
                totalPlayTime += songLength;

                // Add a reference to the song metadata
                songCells.Add(new()
                {
                    AddressRanges = [(songAddress, currentAddress)], // From the beginning of the song to the jump at the end
                    Filters = [CreateFilter('Y', metadataAddress)]
                });

                // Add memory cells for the notes in the songs
                foreach (var currentNoteGroupGroups in noteGroupGroupGroups)
                {
                    var address = currentNoteGroupGroups[0].Address;
                    var memoryCellData = new MemoryCellData([
                        new(VirtualSignalNames.Wait, currentNoteGroupGroups[^1].Address - address),
                        .. currentNoteGroupGroups.Skip(1).Select((group, groupIndex) => new KeyValuePair<string, int>(VirtualSignalNames.LetterOrDigit((char)('A' + groupIndex)), group.Address - address)),
                        .. currentNoteGroupGroups.SelectMany((group, groupIndex) => group.Notes.Select(channelNote => new KeyValuePair<string, int>(SpeakerChannelSignals.AllSignalGroups[groupIndex][channelNote.Channel], channelNote.Note)))
                    ]);

                    if (songDataToCells.TryGetValue(memoryCellData, out var memoryCell))
                    {
                        // Reuse an existing memory cell
                        memoryCell.AddressRanges.Add((address, address));
                    }
                    else
                    {
                        // Create a new memory cell
                        memoryCell = new MemoryCell
                        {
                            Address = address,
                            Filters = memoryCellData.ToFilters()
                        };
                        songCells.Add(memoryCell);
                        songDataToCells[memoryCellData] = memoryCell;
                    }
                }

                // Create a jump back to the beginning of the song
                AddJump(songAddress, isEnabled: song.Loop);

                // Add a pause between songs
                if (!song.Gapless)
                {
                    currentAddress += 120;
                }

                // Add song metadata
                if (song.Name != null)
                {
                    addresses.SongMetadataAddresses[song.Name] = metadataAddress;
                }

                var metadataFilters = new List<Filter>
                {
                    CreateFilter('0', songAddress),
                    CreateFilter('1', trackNumber),
                    CreateFilter('2', (songLength + 59) / 60 * 60) // Round up to the next second
                };

                var displayName = song.DisplayName ?? song.Name;
                if (displayName != null)
                {
                    metadataFilters.AddRange(CreateFiltersForString(displayName, 32, 'A'));
                }

                if (song.Artist != null)
                {
                    metadataFilters.AddRange(CreateFiltersForString(song.Artist, 20, 'I'));
                }

                metadataCells.Add(new MemoryCell { Address = metadataAddress, Filters = metadataFilters });
            }

            // Create a jump back to the beginning of the playlist
            AddJump(playlistAddress, isEnabled: playlist.Loop);
        }

        AddJump(nextAddress);

        var romUsed = songCells.Count;
        var totalRom = width * (height - 1);

        Console.WriteLine($"Total play time: {TimeSpan.FromSeconds(totalPlayTime / 60d):h\\:mm\\:ss\\.fff}");
        Console.WriteLine($"ROM usage: {romUsed}/{totalRom} ({(double)romUsed / totalRom * 100:F1}%)");

        return RomGenerator.Generate(new RomConfiguration
        {
            SnapToGrid = snapToGrid,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            ProgramRows = 1, // Allocate one line for the metadata cells
            ProgramName = "Songs V2",
            IconNames = [ItemNames.ElectronicCircuit, ItemNames.ProgrammableSpeaker]
        }, metadataCells, songCells);
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

    private static Filter CreateFilter(char letterOrDigit, int count) => Filter.Create(VirtualSignalNames.LetterOrDigit(letterOrDigit), count);

    private static Filter CreateFilter(string signalName, int count) => Filter.Create(signalName, count);

    private static IEnumerable<Filter> CreateFiltersForString(string text, int maxCharactersToDisplay, char initialSignal)
    {
        var charactersToDisplay = Math.Min(text.Length, maxCharactersToDisplay);
        var encodedBlock = 0;
        var blockIndex = 0;

        for (var index = 0; index < charactersToDisplay; index++)
        {
            var currentCharacter = (byte)text[index];
            var positionInBlock = index % 4;

            encodedBlock |= currentCharacter << (positionInBlock * 8);

            if (positionInBlock == 3 || index == charactersToDisplay - 1)
            {
                yield return CreateFilter((char)(initialSignal + blockIndex), encodedBlock);
                encodedBlock = 0;
                blockIndex++;
            }
        }
    }

    private class Addresses
    {
        public Dictionary<string, int> PlaylistAddresses { get; } = [];
        public Dictionary<string, int> SongMetadataAddresses { get; } = [];
    }

    private class NoteTuple(ICollection<int> notes) : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() => notes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => notes.GetEnumerator();

        public override bool Equals(object obj)
        {
            return obj is NoteTuple other && notes.SequenceEqual(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var note in notes)
            {
                hash.Add(note);
            }

            return hash.ToHashCode();
        }
    }

    private class MemoryCellData(List<KeyValuePair<string, int>> signals) : IEnumerable<KeyValuePair<string, int>>
    {
        public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => signals.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => signals.GetEnumerator();

        public List<Filter> ToFilters() => [.. signals.Select(entry => Filter.Create(entry.Key, entry.Value))];

        public override bool Equals(object obj)
        {
            return obj is MemoryCellData other && signals.SequenceEqual(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var entry in signals)
            {
                hash.Add(entry.Key);
                hash.Add(entry.Value);
            }

            return hash.ToHashCode();
        }
    }
}

public class MusicBoxConfiguration
{
    public string ConfigFile { get; set; }
    public string OutputBlueprint { get; set; }
    public string OutputJson { get; set; }
    public string OutputConstants { get; set; }
    public string OutputMidiEvents { get; set; }
    public int? Version { get; set; }
    public int? BaseAddress { get; set; }
    public int? BaseNoteAddress { get; set; }
    public int? BaseMetadataAddress { get; set; }
    public int? NextAddress { get; set; }
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
