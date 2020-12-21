using Commons.Music.Midi;
using MusicBoxCompiler.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static MusicBoxCompiler.Constants;
using GMInst = Commons.Music.Midi.GeneralMidi.Instruments;
using Percussions = Commons.Music.Midi.GeneralMidi.Percussions;

namespace MusicBoxCompiler
{
    public static class MidiReader
    {
        // http://www.music.mcgill.ca/~ich/classes/mumt306/StandardMIDIfileformat.html
        private const int ChannelCount = 10;
        private const int PercussionMidiChannel = 9;
        private static readonly List<string> Notes = new List<string> { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private static readonly Dictionary<byte, string> DrumNames =
            typeof(Percussions).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Concat(typeof(PercussionsGM2).GetFields(BindingFlags.Public | BindingFlags.Static))
            .ToDictionary(field => (byte)field.GetValue(null), field => field.Name);
        private static readonly Dictionary<int, Instrument> InstrumentMap = CreateInstrumentMap(
            new InstrumentMapping { Instrument = Instrument.Piano, RangeStart = GMInst.AcousticGrandPiano, RangeEnd = GMInst.Clavi },
            new InstrumentMapping { Instrument = Instrument.Celesta, RangeStart = GMInst.Celesta, RangeEnd = GMInst.MusicBox },
            new InstrumentMapping { Instrument = Instrument.Vibraphone, RangeStart = GMInst.Vibraphone, RangeEnd = GMInst.Dulcimer },
            new InstrumentMapping { Instrument = Instrument.Piano, RangeStart = GMInst.DrawbarOrgan, RangeEnd = GMInst.TangoAccordion },
            new InstrumentMapping { Instrument = Instrument.LeadGuitar, RangeStart = GMInst.AcousticGuitarNylon, RangeEnd = GMInst.Guitarharmonics },
            new InstrumentMapping { Instrument = Instrument.BassGuitar, RangeStart = GMInst.AcousticBass, RangeEnd = GMInst.SynthBass2 },
            new InstrumentMapping { Instrument = Instrument.PluckedStrings, RangeStart = GMInst.Violin, RangeEnd = GMInst.SynthStrings2 },
            new InstrumentMapping { Instrument = Instrument.Celesta, RangeStart = GMInst.ChoirAahs, RangeEnd = GMInst.SynthVoice },
            new InstrumentMapping { Instrument = Instrument.PluckedStrings, RangeStart = GMInst.OrchestraHit, RangeEnd = GMInst.OrchestraHit },
            new InstrumentMapping { Instrument = Instrument.LeadGuitar, RangeStart = GMInst.Trumpet, RangeEnd = GMInst.SynthBrass2 },
            new InstrumentMapping { Instrument = Instrument.Square, RangeStart = GMInst.SopranoSax, RangeEnd = GMInst.Clarinet },
            new InstrumentMapping { Instrument = Instrument.Celesta, RangeStart = GMInst.Piccolo, RangeEnd = GMInst.Ocarina },
            new InstrumentMapping { Instrument = Instrument.Square, RangeStart = GMInst.LeadSquare, RangeEnd = GMInst.LeadSquare },
            new InstrumentMapping { Instrument = Instrument.Sawtooth, RangeStart = GMInst.LeadSawtooth, RangeEnd = GMInst.LeadSawtooth },
            new InstrumentMapping { Instrument = Instrument.Vibraphone, RangeStart = GMInst.LeadCalliope, RangeEnd = GMInst.LeadCalliope },
            new InstrumentMapping { Instrument = Instrument.Square, RangeStart = GMInst.LeadCharang, RangeEnd = GMInst.LeadCharang },
            new InstrumentMapping { Instrument = Instrument.Celesta, RangeStart = GMInst.LeadVoice, RangeEnd = GMInst.LeadVoice },
            new InstrumentMapping { Instrument = Instrument.BassGuitar, RangeStart = GMInst.LeadBassAndLead, RangeEnd = GMInst.LeadBassAndLead },
            new InstrumentMapping { Instrument = Instrument.PluckedStrings, RangeStart = GMInst.PadNewage, RangeEnd = GMInst.PadNewage },
            new InstrumentMapping { Instrument = Instrument.Sawtooth, RangeStart = GMInst.PadPolysynth, RangeEnd = GMInst.PadPolysynth },
            new InstrumentMapping { Instrument = Instrument.SteelDrum, RangeStart = GMInst.FXRain, RangeEnd = GMInst.FXScifi },
            new InstrumentMapping { Instrument = Instrument.LeadGuitar, RangeStart = GMInst.Sitar, RangeEnd = GMInst.Banjo },
            new InstrumentMapping { Instrument = Instrument.SteelDrum, RangeStart = GMInst.TinkleBell, RangeEnd = GMInst.SynthDrum },
            new InstrumentMapping { Instrument = Instrument.Drumkit, RangeStart = GMInst.ReverseCymbal, RangeEnd = GMInst.ReverseCymbal },
            new InstrumentMapping { Instrument = Instrument.BassGuitar, RangeStart = GMInst.GuitarFretNoise, RangeEnd = GMInst.GuitarFretNoise },
            new InstrumentMapping { Instrument = Instrument.SteelDrum, RangeStart = GMInst.Seashore, RangeEnd = GMInst.Seashore }
        );
        private static readonly Dictionary<int, Drum> DrumMap = new Dictionary<int, Drum>
        {
            { PercussionsGM2.HighQ, Drum.HighQ },
            { PercussionsGM2.Slap, Drum.Clap },
            { PercussionsGM2.ScratchPush, Drum.Fx },
            { PercussionsGM2.ScratchPull, Drum.Fx },
            { Percussions.AcousticBassDrum, Drum.Kick1 },
            { Percussions.BassDrum1, Drum.Kick2 },
            { Percussions.SideStick, Drum.Percussion1 },
            { Percussions.AcousticSnare, Drum.Snare1 },
            { Percussions.ElectricSnare, Drum.Snare2 },
            { Percussions.HandClap, Drum.Clap },
            { Percussions.LowFloorTom, Drum.Snare1 },
            { Percussions.ClosedHiHat, Drum.HiHat2 },
            { Percussions.HighFloorTom, Drum.Snare1 },
            { Percussions.PedalHiHat, Drum.HiHat2 },
            { Percussions.LowTom, Drum.Snare3 },
            { Percussions.OpenHiHat, Drum.HiHat2 },
            { Percussions.LowMidTom, Drum.Snare2 },
            { Percussions.HiMidTom, Drum.Snare2 },
            { Percussions.CrashCymbal1, Drum.Crash },
            { Percussions.HighTom, Drum.Snare3 },
            { Percussions.RideCymbal1, Drum.HiHat1 },
            { Percussions.ChineseCymbal, Drum.HiHat1 },
            { Percussions.RideBell, Drum.Cowbell },
            { Percussions.Tambourine, Drum.Shaker },
            { Percussions.SplashCymbal, Drum.HiHat1 },
            { Percussions.Cowbell, Drum.Cowbell },
            { Percussions.CrashCymbal2, Drum.Crash },
            { Percussions.Vibraslap, Drum.Clap },
            { Percussions.RideCymbal2, Drum.HiHat1 },
            { Percussions.HiBongo, Drum.Kick2 },
            { Percussions.LowBongo, Drum.Kick1 },
            { Percussions.MuteHiConga, Drum.Snare2 },
            { Percussions.OpenHiConga, Drum.Snare2 },
            { Percussions.LowConga, Drum.Snare1 },
            { Percussions.Cabasa, Drum.Shaker },
            { Percussions.Maracas, Drum.Shaker },
            { Percussions.ShortWhistle, Drum.Fx },
            { Percussions.LongWhistle, Drum.Fx },
            { Percussions.HiWoodBlock, Drum.Clap },
            { Percussions.LowWoodBlock, Drum.Clap },
            { Percussions.MuteTriangle, Drum.Triangle },
            { Percussions.OpenTriangle, Drum.Triangle },
            { PercussionsGM2.Shaker, Drum.Shaker },
            { PercussionsGM2.JingleBell, Drum.Shaker },
            { PercussionsGM2.Belltree, Drum.Cowbell },
            { 87, Drum.Percussion1 },
            { 88, Drum.Percussion1 },
            { 89, Drum.Percussion1 },
            { 91, Drum.Percussion1 }
        };
        private static readonly Dictionary<Instrument, int> BaseInstrumentOffsets = new Dictionary<Instrument, int>
        {
            { Instrument.Piano, -52 },
            { Instrument.BassGuitar, -40 },
            { Instrument.LeadGuitar, -40 },
            { Instrument.Sawtooth, -40 },
            { Instrument.Square, -40 },
            { Instrument.Celesta, -76 },
            { Instrument.Vibraphone, -76 },
            { Instrument.PluckedStrings, -64 },
            { Instrument.SteelDrum, -52 }
        };
        private static readonly Dictionary<Instrument, double> BaseInstrumentVolumes = new Dictionary<Instrument, double>
        {
            { Instrument.Square, 0.5 },
            { Instrument.Sawtooth, 0.75 }
        };

        private static Dictionary<int, Instrument> CreateInstrumentMap(params InstrumentMapping[] mappings)
        {
            return mappings.SelectMany(mapping => Enumerable.Range(mapping.RangeStart, mapping.RangeEnd - mapping.RangeStart + 1)
                .Select(channel => (channel, mapping.Instrument)))
                .ToDictionary(entry => entry.channel, entry => entry.Instrument);
        }

        public static Song ReadSong(string midiFile, bool debug, Dictionary<Instrument, int> instrumentOffsets, double masterVolume, Dictionary<Instrument, double> instrumentVolumes)
        {
            const int unreasonablyHighOctave = 12; // This is to ensure that we don't have a negative number before calculating the octave, which would throw off the result

            var midiEventStream = debug ? new MemoryStream() : null;
            var midiEventWriter = debug ? new StreamWriter(midiEventStream) : null;

            midiEventWriter?.WriteLine(midiFile);

            var (midiNotes, totalPlayTime) = ReadMidiFile(midiFile);

            midiEventWriter?.WriteLine($"Total play time: {totalPlayTime}");

            if (instrumentOffsets == null)
            {
                instrumentOffsets = midiNotes
                    .Where(note => note.Instrument is not Instrument.Unknown and not Instrument.Drumkit)
                    .GroupBy(note => note.Instrument, note => note.RelativeNoteNumber, (instrument, noteNumbers) =>
                    {
                        var octaves = noteNumbers.GroupBy(relativeNoteNumber => (relativeNoteNumber - 1 + unreasonablyHighOctave * 12) / 12 - unreasonablyHighOctave, (octaveNumber, groupedNoteNumbers) => (OctaveNumber: octaveNumber, NoteCount: groupedNoteNumbers.Count()));
                        var totalNotes = octaves.Sum(octave => octave.NoteCount);
                        var minOctave = octaves.Min(octave => octave.OctaveNumber);
                        var maxOctave = octaves.Max(octave => octave.OctaveNumber);
                        var minOctaveShift = Math.Min(minOctave, 0);
                        var maxOctaveShift = Math.Max(maxOctave - 3, 0);

                        const double newNotesOutOfRangePenalty = 4;
                        const double octaveShiftPenaltyBase = 0.05;
                        const double octaveShiftPenaltyGrowth = 2;

                        return (
                            Instrument: instrument,
                            NoteShift: minOctaveShift < 0 || maxOctaveShift > 0
                                ? Enumerable.Range(minOctaveShift, maxOctaveShift - minOctaveShift + 1)
                                    .Select(octavesOutOfRange =>
                                    {
                                        var octavesThatAreOutOfRange = octaves.Where(octave => octave.OctaveNumber - octavesOutOfRange is < 0 or > 3);

                                        return (
                                            OctaveShift: -octavesOutOfRange,
                                            NotesStillOutOfRange: octavesThatAreOutOfRange.Where(octave => octave.OctaveNumber is < 0 or > 3).Sum(octave => octave.NoteCount),
                                            NewNotesOutOfRange: octavesThatAreOutOfRange.Where(octave => octave.OctaveNumber is not (< 0 or > 3)).Sum(octave => octave.NoteCount)
                                        );
                                    })
                                    // Order by the number of notes still out of range, imposing a penalty for each octave we shift to favor shifting less even if a few notes are left behind
                                    .OrderBy(tuple => (tuple.NotesStillOutOfRange + tuple.NewNotesOutOfRange * newNotesOutOfRangePenalty) / totalNotes + (tuple.OctaveShift != 0 ? octaveShiftPenaltyBase * Math.Pow(octaveShiftPenaltyGrowth, Math.Abs(tuple.OctaveShift) - 1) : 0))
                                    .First()
                                    .OctaveShift * 12
                                : 0
                        );
                    })
                    .ToDictionary(tuple => tuple.Instrument, tuple => tuple.NoteShift);

                midiEventWriter?.WriteLine($"Calculated instrument offsets: {string.Join(", ", instrumentOffsets.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}: {entry.Value}"))}");
            }

            if (midiEventWriter != null)
            {
                var instrumentsNotMapped = midiNotes.Where(note => note.Instrument == Instrument.Unknown).GroupBy(note => note.OriginalInstrumentName, (originalInstrumentName, notes) => originalInstrumentName).ToList();
                if (instrumentsNotMapped.Count > 0)
                {
                    midiEventWriter.WriteLine($"Instruments not mapped: {string.Join(", ", instrumentsNotMapped)}");
                }

                var percussionsNotMapped = midiNotes.Where(note => note.Instrument == Instrument.Drumkit && note.RelativeNoteNumber == 0).OrderBy(note => note.RelativeNoteNumber).Select(note => note.OriginalNoteName).Distinct().ToList();
                if (percussionsNotMapped.Count > 0)
                {
                    midiEventWriter.WriteLine($"Percussions not mapped: {string.Join(", ", percussionsNotMapped)}");
                }

                var instrumentsInSong = midiNotes.Where(note => note.Instrument != Instrument.Unknown).GroupBy(note => note.Instrument, (instrument, notes) => instrument).ToList();
                if (instrumentsInSong.Count > 0)
                {
                    midiEventWriter.WriteLine($"Instruments in song: {string.Join(", ", instrumentsInSong)}");
                }
            }

            var lastTime = TimeSpan.Zero;
            var currentNotes = new List<Note>();
            var noteGroups = new List<NoteGroup>();

            foreach (var midiNote in midiNotes)
            {
                var instrument = midiNote.Instrument;
                var isInstrumentMapped = instrument != Instrument.Unknown;
                var isNoteInRange = true;
                var playedNote = 0;
                var volume = 0d;

                if (isInstrumentMapped)
                {
                    var baseNoteOffset = BaseInstrumentOffsets.TryGetValue(instrument, out var baseOffsetValue) ? baseOffsetValue : 0;
                    var noteOffset = instrumentOffsets != null && instrumentOffsets.TryGetValue(instrument, out var offsetValue) ? offsetValue : 0;
                    var baseInstrumentVolume = BaseInstrumentVolumes.TryGetValue(instrument, out var baseInstrumentVolumeValue) ? baseInstrumentVolumeValue : 1;
                    var instrumentVolume = instrumentVolumes != null && instrumentVolumes.TryGetValue(instrument, out var instrumentVolumeValue) ? instrumentVolumeValue : 1;
                    var timeDelta = midiNote.CurrentTime - lastTime;

                    playedNote = instrument == Instrument.Drumkit ? midiNote.RelativeNoteNumber : midiNote.OriginalNoteNumber + noteOffset;
                    var effectiveNoteNumber = midiNote.RelativeNoteNumber + noteOffset;
                    isNoteInRange = effectiveNoteNumber > 0 && effectiveNoteNumber <= 48;
                    volume = Math.Min(midiNote.Volume * baseInstrumentVolume * instrumentVolume * masterVolume, 1);

                    if (isNoteInRange)
                    {
                        if (timeDelta >= TimeSpan.FromMilliseconds(17) || currentNotes.Count >= ChannelCount) // 17 is 1000ms / 60fps, rounded up
                        {
                            noteGroups.Add(new NoteGroup { Notes = currentNotes, Length = 4 / timeDelta.TotalMinutes, BeatsPerMinute = 1 });
                            currentNotes = new List<Note>();
                            lastTime = midiNote.CurrentTime;
                        }

                        var duplicateNote = currentNotes.Find(note => note.Instrument == instrument && note.Number == effectiveNoteNumber);

                        if (duplicateNote != null)
                        {
                            duplicateNote.Volume = Math.Max(volume, duplicateNote.Volume);
                        }
                        else
                        {
                            currentNotes.Add(new Note
                            {
                                Instrument = instrument,
                                Number = effectiveNoteNumber,
                                Pitch = playedNote - (instrument != Instrument.Drumkit ? 40 : 0),
                                Volume = volume
                            });
                        }
                    }
                }

                if (midiEventWriter != null)
                {
                    var instrumentAndNote = instrument switch
                    {
                        Instrument.Drumkit => isNoteInRange ? Drums[playedNote - 1] : "Unknown",
                        _ => $"{instrument} {Notes[(playedNote + unreasonablyHighOctave * Notes.Count) % Notes.Count]}{playedNote / Notes.Count - 1}"
                    };

                    midiEventWriter.WriteLine($"{lastTime:mm\\:ss\\.fff}: {midiNote.OriginalInstrumentName} {midiNote.OriginalNoteName} volume {midiNote.Volume:F2}" +
                        (isInstrumentMapped ? $" => {instrumentAndNote} volume {volume:F2}" : "") +
                        (!isNoteInRange ? " (note not in range)" : "") +
                        (!isInstrumentMapped ? " (instrument not mapped)" : ""));
                }
            }

            if (currentNotes.Count > 0)
            {
                var currentTime = totalPlayTime;
                var timeDelta = currentTime - lastTime;

                noteGroups.Add(new NoteGroup { Notes = currentNotes, Length = 4 / timeDelta.TotalMinutes, BeatsPerMinute = 1 });
            }

            midiEventWriter?.WriteLine();
            midiEventWriter?.Flush();

            return new Song
            {
                NoteGroups = noteGroups,
                DebugStream = midiEventStream
            };
        }

        private static (List<MidiNote> MidiNotes, TimeSpan TotalPlayTime) ReadMidiFile(string midiFile)
        {
            using var fileReader = File.OpenRead(midiFile);
            var music = SmfTrackMerger.Merge(MidiMusic.Read(fileReader));
            var machine = new MidiMachine();

            var tempo = MidiMetaType.DefaultTempo;
            var currentTimeMillis = 0d;
            var lastNoteTimeMillis = 0d;
            var notes = new List<MidiNote>();

            foreach (var midiMessage in music.Tracks[0].Messages)
            {
                var midiEvent = midiMessage.Event;
                var channel = machine.Channels[midiEvent.Channel];

                currentTimeMillis += tempo / 1000d * midiMessage.DeltaTime / music.DeltaTimeSpec;

                if (midiEvent.EventType == MidiEvent.Meta && midiEvent.Msb == MidiMetaType.Tempo)
                {
                    tempo = MidiMetaType.GetTempo(midiEvent.ExtraData, midiEvent.ExtraDataOffset);
                }

                machine.ProcessEvent(midiEvent);

                if (midiEvent.EventType == MidiEvent.NoteOn)
                {
                    var velocity = midiEvent.Lsb;

                    if (velocity > 0)
                    {
                        var noteNumber = midiEvent.Msb;
                        var isPercussion = midiEvent.Channel == PercussionMidiChannel;
                        var instrument = isPercussion
                            ? Instrument.Drumkit
                            : InstrumentMap.TryGetValue(channel.Program, out var instrumentValue) ? instrumentValue : Instrument.Unknown;
                        var instrumentName = (isPercussion ? GeneralMidi.DrumKitsGM2.ElementAtOrDefault(channel.Program) : GeneralMidi.InstrumentNames.ElementAtOrDefault(channel.Program)) ?? channel.Program.ToString();
                        var noteName = isPercussion
                            ? DrumNames.TryGetValue(noteNumber, out var drumName) ? drumName : noteNumber.ToString()
                            : $"{Notes[noteNumber % Notes.Count]}{noteNumber / Notes.Count - 1}";

                        var note = new MidiNote
                        {
                            OriginalInstrumentName = instrumentName,
                            OriginalNoteName = noteName,
                            OriginalNoteNumber = noteNumber,
                            Instrument = instrument,
                            Volume = velocity / 127d,
                            CurrentTime = TimeSpan.FromMilliseconds(currentTimeMillis)
                        };

                        if (instrument != Instrument.Unknown)
                        {
                            var baseNoteOffset = BaseInstrumentOffsets.TryGetValue(instrument, out var baseOffsetValue) ? baseOffsetValue : 0;

                            var relativeNoteNumber = isPercussion
                                ? DrumMap.TryGetValue(noteNumber, out var drum) ? (int)drum : 0
                                : instrument == Instrument.Drumkit
                                    ? (int)Drum.ReverseCymbal
                                    : noteNumber + baseNoteOffset;

                            note = note with { RelativeNoteNumber = relativeNoteNumber };
                        }

                        notes.Add(note);
                        lastNoteTimeMillis = currentTimeMillis;
                    }
                }
            }

            var maxEndOfSongSilenceMillis = 3000; // Truncate the song if the end is more than 3 seconds after the last note
            var totalPlayTime = TimeSpan.FromMilliseconds(Math.Min(currentTimeMillis, lastNoteTimeMillis + maxEndOfSongSilenceMillis));

            return (notes, totalPlayTime);
        }

        private record MidiNote
        {
            public string OriginalInstrumentName { get; init; }
            public string OriginalNoteName { get; init; }
            public int OriginalNoteNumber { get; init; }
            public Instrument Instrument { get; init; }
            public int RelativeNoteNumber { get; init; }
            public double Volume { get; init; }
            public TimeSpan CurrentTime { get; init; }
        }
    }
}
