using BlueprintCommon.Models;
using BlueprintGenerator;
using MusicBoxCompiler.Models;
using MusicBoxCompiler.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicBoxCompiler.SongCompilers;

public class SongCompilerV1 : ISongCompiler
{
    private const int InstrumentCount = 12;
    private const int NoteGroupAddressBits = 16;
    private const int NoteGroupTimeOffsetBits = 11;
    private const int MetadataAddressBits = 10;

    public int? MaxConcurrentNotes => 10;

    public bool SupportsSustainedNotes => false;

    public CompiledSongs CompileSongs(List<Playlist> playlists, MusicBoxConfiguration configuration)
    {
        var baseAddress = configuration.BaseAddress ?? 1;
        var baseNoteAddress = configuration.BaseNoteAddress ?? 1 << NoteGroupAddressBits;
        var baseMetadataAddress = configuration.BaseMetadataAddress ?? 1;
        var nextAddress = configuration.NextAddress ?? baseAddress;
        var volumeLevels = configuration.VolumeLevels ?? 10;
        var minVolume = configuration.MinVolume ?? 0.1;
        var maxVolume = configuration.MaxVolume ?? 1;

        var songCells = new List<MemoryCell>();
        var noteGroupCells = new List<MemoryCell>();
        var metadataCells = new List<MemoryCell>();
        var allNoteTuples = new HashSet<NoteTuple<int>>();
        var noteTuplesToAddresses = new Dictionary<NoteTuple<int>, (int Address, int SubAddress)>();
        Dictionary<string, int> playlistAddresses = [];
        Dictionary<string, int> songMetadataAddresses = [];
        var currentAddress = baseAddress;
        var maxFilters = 40;
        var totalPlayTime = 0;

        Filter CreateJumpFilter(int targetAddress)
        {
            return Filter.Create('U', targetAddress - (currentAddress + 3));
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

        NoteTuple<int> CreateNoteTuple(NoteGroup noteGroup)
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

        var noteGroupCellDataByFreeSpace = Enumerable.Range(0, maxFilters).Select(index => new Queue<List<NoteTuple<int>>>()).ToList();

        foreach (var noteTuple in noteTuples)
        {
            if (!allNoteTuples.Add(noteTuple))
            {
                continue;
            }

            var spaceRequired = noteTuple.Count();

            for (int freeSpace = spaceRequired; ; freeSpace++)
            {
                List<NoteTuple<int>> noteGroupCellData = null;
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
                var noteGroupFilters = noteTuple.Select((note, index) => Filter.Create(noteGroupSignals.NoteSignals[index], note)).ToList();

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
                playlistAddresses[playlist.Name] = playlistAddress;
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
                        currentFilters.Add(Filter.Create('Y', metadataAddress + ((currentTimeOffset + 1) << MetadataAddressBits)));
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

                    var length = (int)Math.Round(noteGroupLength.TotalSeconds * 60) - timeDeficit;

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
                    currentFilters.Add(Filter.Create(signal, noteGroupAddress + (noteGroupTimeOffset << NoteGroupAddressBits) + (noteGroupSubAddress << (NoteGroupAddressBits + NoteGroupTimeOffsetBits))));

                    currentTimeOffset += length;

                    if (currentFilters.Count >= maxFilters)
                    {
                        var minimumCellLength = maxFilters + 1; // The number of cycles required to finish loading all of the note groups
                        var cellLength = currentTimeOffset - cellStartTime;

                        if (cellLength < minimumCellLength)
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
                if (song.Name is not null)
                {
                    songMetadataAddresses[song.Name] = metadataAddress;
                }

                var metadataFilters = new List<Filter>
                {
                    Filter.Create('0', songAddress),
                    Filter.Create('1', trackNumber),
                    Filter.Create('2', (songLength + 59) / 60 * 60) // Round up to the next second
                };

                var displayName = song.DisplayName ?? song.Name;
                if (displayName is not null)
                {
                    metadataFilters.AddRange(FilterUtils.CreateFiltersForString(displayName, 32, 'A'));
                }

                if (song.Artist is not null)
                {
                    metadataFilters.AddRange(FilterUtils.CreateFiltersForString(song.Artist, 20, 'I'));
                }

                metadataCells.Add(new MemoryCell { Address = metadataAddress, Filters = metadataFilters });
            }

            // Create a jump back to the beginning of the playlist
            AddJump(playlistAddress, isEnabled: playlist.Loop);
        }

        AddJump(nextAddress);

        songCells.AddRange(noteGroupCells);

        return new CompiledSongs
        {
            TotalPlayTime = totalPlayTime,
            SongCells = songCells,
            MetadataCells = metadataCells,
            PlaylistAddresses = playlistAddresses,
            SongMetadataAddresses = songMetadataAddresses
        };
    }
}
