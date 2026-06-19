using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator;
using BlueprintGenerator.Constants;
using BlueprintGenerator.MusicBox;
using MusicBoxCompiler.Models;
using MusicBoxCompiler.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MusicBoxCompiler.SongCompilers;

public class SongCompilerV2 : ISongCompiler
{
    private const int InstrumentCount = 10;
    private const int PitchCount = 48;
    private const int VolumeCount = 100;
    private static readonly int ChannelCount = MusicBoxSignals.SpeakerChannelSignals.Count;
    private const int LyricOffsetBits = 8;
    private const int LyricLineWidth = 56;
    private const int MaximumNoteDuration = 44000;
    private const int ChannelCooldownTicks = 1;
    private const int SongGapTicks = 120;

    private static readonly List<int> NoteGroupsBySize = [
        .. Enumerable.Range(1, ChannelCount)
            .Select(size => MusicBoxSignals.AllNoteGroupSignals
                .FindLastIndex(noteGroupSignals => noteGroupSignals.Count >= size))
    ];

    public int? MaxConcurrentNotes => null;

    public bool SupportsSustainedNotes => true;

    public CompiledSongs CompileSongs(List<Playlist> playlists, MusicBoxConfiguration configuration)
    {
        var baseAddress = configuration.BaseAddress ?? 1;
        var baseNoteAddress = configuration.BaseNoteAddress ?? 1 << MusicBoxV2DecoderGenerator.NoteGroupAddressBits;

        List<MemoryCell> songCells = [];
        List<MemoryCell> metadataCells = [];
        List<List<NoteGroupReference>> noteGroupReferenceGroups = [];
        Dictionary<NoteGroupKey, List<ChannelizedNoteGroup>> noteGroupsByKey = [];
        Stack<ChannelizedNoteGroup>[] noteGroupsBySubAddress = [.. Enumerable.Range(0, MusicBoxSignals.AllNoteGroupSignals.Count).Select(_ => new Stack<ChannelizedNoteGroup>())];
        var channelRemainingTimes = new int[ChannelCount];
        Dictionary<string, int> playlistAddresses = [];
        Dictionary<string, int> songMetadataAddresses = [];
        var currentAddress = baseAddress;
        var totalPlayTime = 0;
        ChannelizedNoteGroup emptyNoteGroupData = new(new(new([]), null), []) { Address = 1 };

        Filter AddJump(int targetAddress)
        {
            var jumpFilter = Filter.Create('U', targetAddress - (currentAddress + 3));
            songCells.Add(new() { Address = currentAddress, Filters = [jumpFilter] });
            currentAddress += 4;
            return jumpFilter;
        }

        int EncodeDuration(Note note) =>
            Math.Min(
                Math.Max(
                    (int)double.Ceiling(note.Duration.TotalSeconds * 60),
                    Math.Min(AudioClipInfo.GetAudioClipLength(note), MusicBoxV2SpeakerGenerator.NoteTrailOffTicks)),
                MaximumNoteDuration);

        int EncodeVolume(Note note) => Math.Min(Math.Max((int)double.Round(note.Volume * 100), 1), 100) - 1;

        int EncodeVolumeChange(Note note) => EncodeVolume(note) - EncodeVolume(note.PreviousNote);

        int EncodeNote(Note note)
        {
            var encodedDuration = EncodeDuration(note);
            var encodedInstrument = (int)note.Instrument - 3;
            var encodedPitch = note.Number - 1;
            var encodedVolume = EncodeVolume(note);

            return encodedVolume + (encodedPitch + (encodedInstrument + encodedDuration * InstrumentCount) * PitchCount) * VolumeCount;
        }

        int EncodeNoteGroupReference(int noteGroupAddress, int subAddress, int timeOffset)
        {
            Debug.Assert(noteGroupAddress >= baseAddress + 4 && noteGroupAddress < baseNoteAddress || noteGroupAddress == 1, "Note group address out of range");
            Debug.Assert(subAddress >= 0 && subAddress < MusicBoxSignals.AllNoteGroupSignals.Count, "Sub-address out of range");
            Debug.Assert(timeOffset is >= 0 and < (1 << MusicBoxV2DecoderGenerator.NoteGroupTimeOffsetBits), "Time offset out of range");

            return noteGroupAddress + (timeOffset << MusicBoxV2DecoderGenerator.NoteGroupAddressBits) + (subAddress << (MusicBoxV2DecoderGenerator.NoteGroupAddressBits + MusicBoxV2DecoderGenerator.NoteGroupTimeOffsetBits));
        }

        int EncodeLyricMetadata(int offset, bool isStartOfLine)
        {
            Debug.Assert(offset >= 0 && offset < (1 << LyricOffsetBits) - 1, "Lyric offset out of range");

            var encodedOffset = offset + 1;

            return encodedOffset + (isStartOfLine ? 1 << LyricOffsetBits : 0);
        }

        long EncodeNoteTuple(Note note)
        {
            var (encodedNote, encodedPreviousChannel) = note.PreviousNote is not null
                ? (EncodeVolumeChange(note), note.PreviousNote.Channel.Value + 1)
                : (EncodeNote(note), 0);

            return encodedNote + (encodedPreviousChannel << 32);
        }

        NoteTuple<long> CreateNoteTuple(IEnumerable<Note> notes) => new([.. notes.Select(EncodeNoteTuple).Order()]);

        AddJump(baseNoteAddress);
        var baseNoteGroupAddress = currentAddress;
        currentAddress = baseNoteAddress;

        // Add the songs
        foreach (var (playlistIndex, playlist) in playlists.Index())
        {
            var playlistAddress = currentAddress;

            if (playlist.Name is not null)
            {
                playlistAddresses[playlist.Name] = playlistAddress;
            }

            foreach (var (songIndex, song) in playlist.Songs.Index())
            {
                var metadataAddress = song.MetadataAddress;
                List<NoteGroupReference> noteGroupReferences = [];
                string currentLyrics = null;
                var timeDeficit = 0;

                // Add a pause before each song that can be skipped for gapless playback
                currentAddress += SongGapTicks;

                var songAddress = currentAddress;

                void StartNewNoteGroupReferenceGroup()
                {
                    noteGroupReferenceGroups.Add(noteGroupReferences);
                    noteGroupReferences = [];
                }

                // Add the notes for the song
                foreach (var noteGroup in song.NoteGroups)
                {
                    // Strip leading silence
                    if (noteGroup.Notes.Count == 0)
                    {
                        continue;
                    }

                    var noteGroupLength = (int)Math.Round(noteGroup.Length.TotalSeconds * 60) - timeDeficit;

                    // We can't have multiple note groups play too close to each other.
                    // However, to avoid delaying future notes we capture the amount that the length is adjusted
                    // as the time deficit and apply that to the next note.
                    const int minimumLength = 1;
                    if (noteGroupLength < minimumLength)
                    {
                        timeDeficit = minimumLength - noteGroupLength;
                        noteGroupLength = minimumLength;
                    }
                    else
                    {
                        timeDeficit = 0;
                    }

                    // Propagate channels for sustained notes
                    foreach (var note in noteGroup.Notes)
                    {
                        note.Channel = note.PreviousNote?.Channel;
                    }

                    var sustainedNotes = noteGroup.Notes
                        .Where(note => note.Channel is not null && EncodeVolumeChange(note) != 0)
                        .ToList();

                    var availableChannelCount = channelRemainingTimes.Count(channelTime => channelTime == 0);
                    var newNotes = noteGroup.Notes
                        .Where(note => note.PreviousNote is null)
                        .OrderBy(note => note.Instrument)
                        .ThenBy(note => note.Number)
                        .Take(availableChannelCount)
                        .ToList();

                    if (sustainedNotes.Count > 0 || newNotes.Count > 0 || noteGroup.Lyrics is not null)
                    {
                        LyricInfo lyricInfo = null;

                        if (noteGroup.Lyrics is not null)
                        {
                            var isStartOfLine = noteGroup.IsStartOfLine || (currentLyrics?.Length ?? 0) + noteGroup.Lyrics.Length > LyricLineWidth;
                            if (isStartOfLine)
                            {
                                currentLyrics = null;
                            }

                            // Lyrics are packed into integers and can therefore only be shifted by multiples of 4 characters.
                            // This means that if the existing line is not a multiple of 4 characters, the last few need to get included in the lyrics.
                            var lyricOffset = currentLyrics is not null ? currentLyrics.Length / 4 : 0;
                            var lyrics = (currentLyrics is not null ? currentLyrics[(lyricOffset * 4)..] : "") + noteGroup.Lyrics;
                            lyricInfo = new(lyrics, lyricOffset, isStartOfLine);
                            currentLyrics += noteGroup.Lyrics;
                        }

                        var noteGroupKey = new NoteGroupKey(CreateNoteTuple([.. sustainedNotes, .. newNotes]), lyricInfo);

                        if (!noteGroupsByKey.TryGetValue(noteGroupKey, out var noteGroupsWithKey))
                        {
                            noteGroupsWithKey = [];
                            noteGroupsByKey[noteGroupKey] = noteGroupsWithKey;
                        }

                        var channelizedNoteGroup = noteGroupsWithKey.FirstOrDefault(noteGroup => noteGroup.Notes.All(channelNote => channelRemainingTimes[channelNote.Channel] == 0));
                        var channelNotes = new int[ChannelCount];

                        foreach (var note in sustainedNotes)
                        {
                            // Add the volume change to the channel
                            channelNotes[note.Channel.Value] += EncodeVolumeChange(note);
                        }

                        foreach (var note in newNotes)
                        {
                            var encodedNote = EncodeNote(note);

                            // Find the next available channel
                            var channelIndex = channelizedNoteGroup is not null
                                ? channelizedNoteGroup.Notes.FirstOrDefault(n => n.Note == encodedNote)?.Channel ?? -1
                                : Array.IndexOf(channelRemainingTimes, 0);

                            Debug.Assert(channelIndex >= 0);

                            // Allocate time on the channel
                            var noteDuration = EncodeDuration(note);
                            channelRemainingTimes[channelIndex] = noteDuration + ChannelCooldownTicks;

                            // Add the note to the channel
                            note.Channel = channelIndex;
                            channelNotes[channelIndex] = encodedNote;
                        }

                        if (channelizedNoteGroup is null)
                        {
                            var notes = channelNotes
                                .Select((note, channelIndex) => new ChannelNote(channelIndex, note))
                                .Where(note => note.Note != 0)
                                .ToList();

                            Debug.Assert(notes.Count > 0 || noteGroup.Lyrics is not null);

                            var size = notes.Count > 0 ? notes.Max(note => note.Channel) : 1;
                            var maxSubAddress = NoteGroupsBySize[size];

                            channelizedNoteGroup = new(noteGroupKey, notes);
                            noteGroupsWithKey.Add(channelizedNoteGroup);
                            noteGroupsBySubAddress[maxSubAddress].Push(channelizedNoteGroup);
                        }

                        // Start a new note group reference group if the time offset gets too big to encode
                        if (noteGroupReferences.Count > 0 && currentAddress - noteGroupReferences[0].Address - noteGroupReferences.Count + 1 >= (1 << MusicBoxV2DecoderGenerator.NoteGroupTimeOffsetBits))
                        {
                            // Fill up the current note group reference group to ensure that all registers in the decoder get loaded instead of being left with the previous values
                            for (var index = noteGroupReferences.Count; index < MusicBoxSignals.NoteGroupReferenceSignals.Count; index++)
                            {
                                noteGroupReferences.Add(new(noteGroupReferences[0].Address + noteGroupReferences.Count - 2 + (1 << MusicBoxV2DecoderGenerator.NoteGroupTimeOffsetBits), emptyNoteGroupData));
                            }

                            StartNewNoteGroupReferenceGroup();
                        }

                        noteGroupReferences.Add(new(currentAddress, channelizedNoteGroup));

                        Debug.Assert(noteGroupReferences.Count <= MusicBoxSignals.NoteGroupReferenceSignals.Count);

                        if (noteGroupReferences.Count == MusicBoxSignals.NoteGroupReferenceSignals.Count)
                        {
                            var minimumCellLength = MusicBoxSignals.NoteGroupReferenceSignals.Count + 1; // The number of cycles required to finish loading all of the note groups
                            var cellLength = currentAddress + noteGroupLength - noteGroupReferences[0].Address;

                            if (cellLength < minimumCellLength)
                            {
                                var extension = minimumCellLength - cellLength;
                                noteGroupLength += extension;
                                timeDeficit += extension;
                            }

                            StartNewNoteGroupReferenceGroup();
                        }
                    }

                    currentAddress += noteGroupLength;

                    // Reduce channel remaining times
                    for (var channel = 0; channel < ChannelCount; channel++)
                    {
                        channelRemainingTimes[channel] = Math.Max(0, channelRemainingTimes[channel] - noteGroupLength);
                    }
                }

                if (noteGroupReferences.Count > 0)
                {
                    StartNewNoteGroupReferenceGroup();
                }

                var songLength = currentAddress - songAddress;
                totalPlayTime += songLength;

                var endOfSongAddress = currentAddress - 1;

                // Add a reference to the song metadata
                songCells.Add(new()
                {
                    AddressRanges = [(songAddress - SongGapTicks, endOfSongAddress)],
                    Filters = [Filter.Create('Y', metadataAddress)]
                });

                // Add a gap at the end of the song to allow time for processing
                currentAddress += 4;

                // Add song metadata
                if (song.Name is not null)
                {
                    songMetadataAddresses[song.Name] = metadataAddress;
                }

                var nextSongMetadataAddress = song.Loop ? metadataAddress // Same song
                    : songIndex < playlist.Songs.Count - 1 ? playlist.Songs[songIndex + 1].MetadataAddress // Next song in the playlist
                    : playlist.Loop ? playlist.Songs[0].MetadataAddress // Beginning of the playlist
                    : playlistIndex < playlists.Count - 1 ? playlists[playlistIndex + 1].Songs[0].MetadataAddress // Next playlist
                    : playlists[0].Songs[0].MetadataAddress; // Wrap around to the beginning

                List<Filter> metadataFilters =
                [
                    Filter.Create('0', songAddress),
                    Filter.Create('1', metadataAddress), // Use the metadata address as the track number
                    Filter.Create('2', (songLength + 59) / 60 * 60), // Round up to the next second
                    Filter.Create(VirtualSignalNames.Output, endOfSongAddress),
                    Filter.Create(VirtualSignalNames.DownRightArrow, nextSongMetadataAddress),
                    Filter.Create(VirtualSignalNames.RightArrow, song.Gapless ? 1 : 0),
                ];

                var displayName = song.DisplayName ?? song.Name;
                if (displayName is not null)
                {
                    metadataFilters.AddRange(FilterUtils.CreateFiltersForString(displayName, 64, 'A'));
                }

                if (song.Album is not null)
                {
                    metadataFilters.AddRange(FilterUtils.CreateFiltersForString(song.Album, 36, 'Q'));
                }

                if (song.Artist is not null)
                {
                    metadataFilters.AddRange(FilterUtils.CreateFiltersForString(song.Artist, 24, '3'));
                }

                metadataCells.Add(new MemoryCell { Address = metadataAddress, Filters = metadataFilters });
            }
        }

        AddJump(baseAddress);

        // Rebalance note groups, moving note groups from sub-addresses with more note groups to smaller-numbered sub-addresses (which support larger note groups) that have fewer note groups
        while (true)
        {
            var (subAddressToMoveFrom, noteGroupsToMoveFrom) = noteGroupsBySubAddress.Index().MaxBy(tuple => tuple.Item.Count);

            int subAddressToMoveTo;
            for (subAddressToMoveTo = subAddressToMoveFrom - 1; subAddressToMoveTo >= 0; subAddressToMoveTo--)
            {
                if (noteGroupsBySubAddress[subAddressToMoveTo].Count < noteGroupsToMoveFrom.Count)
                {
                    break;
                }
            }

            if (subAddressToMoveTo < 0)
            {
                break;
            }

            var noteGroupToMove = noteGroupsBySubAddress[subAddressToMoveFrom].Pop();
            noteGroupsBySubAddress[subAddressToMoveTo].Push(noteGroupToMove);
        }

        List<MemoryCell> noteGroupCells = [];

        // Add memory cells for the note groups
        foreach (var (subAddress, noteGroups) in noteGroupsBySubAddress.Index())
        {
            foreach (var (noteGroupCellIndex, noteGroup) in noteGroups.Index())
            {
                var address = baseNoteGroupAddress + noteGroupCellIndex;

                noteGroup.Address = address;
                noteGroup.SubAddress = subAddress;

                var noteGroupSignals = MusicBoxSignals.AllNoteGroupSignals[subAddress];
                var noteGroupFilters = noteGroup.Notes.Select(note => Filter.Create(noteGroupSignals[note.Channel], note.Note)).ToList();

                if (noteGroup.Key.LyricInfo is not null)
                {
                    var lyricSignals = MusicBoxSignals.AllLyricSignals[subAddress];
                    noteGroupFilters.Add(Filter.Create(lyricSignals[0], EncodeLyricMetadata(noteGroup.Key.LyricInfo.LyricOffset, noteGroup.Key.LyricInfo.IsStartOfLine)));
                    noteGroupFilters.AddRange(FilterUtils.CreateFiltersForString(noteGroup.Key.LyricInfo.Lyrics, 16, lyricSignals[1..]));
                }

                if (noteGroupCellIndex < noteGroupCells.Count)
                {
                    noteGroupCells[noteGroupCellIndex].Filters.AddRange(noteGroupFilters);
                }
                else
                {
                    Debug.Assert(noteGroupCellIndex == noteGroupCells.Count);
                    noteGroupCells.Add(new MemoryCell { Address = address, Filters = noteGroupFilters });
                }
            }
        }

        Dictionary<MemoryCellData, MemoryCell> songDataToCells = [];

        // Add memory cells for the note group reference groups
        foreach (var currentReferenceGroup in noteGroupReferenceGroups)
        {
            var address = currentReferenceGroup[0].Address;
            var memoryCellData = new MemoryCellData([
                .. currentReferenceGroup.Select((reference, index) => new KeyValuePair<string, int>(
                    MusicBoxSignals.NoteGroupReferenceSignals[index],
                    EncodeNoteGroupReference(reference.NoteGroup.Address, reference.NoteGroup.SubAddress, reference.Address - address - index + 1)))
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

    private record NoteGroupReference(int Address, ChannelizedNoteGroup NoteGroup);

    private record ChannelizedNoteGroup(NoteGroupKey Key, List<ChannelNote> Notes)
    {
        public int Address { get; set; }
        public int SubAddress { get; set; }
    }

    private record NoteGroupKey(NoteTuple<long> NoteTuple, LyricInfo LyricInfo);

    private record LyricInfo(string Lyrics = null, int LyricOffset = 0, bool IsStartOfLine = false);

    private record ChannelNote(int Channel, int Note);

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
