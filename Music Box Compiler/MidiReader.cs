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
            new InstrumentMapping { Instrument = Instrument.LeadGuitar, RangeStart = GMInst.Trumpet, RangeEnd = GMInst.SynthBrass2 },
            new InstrumentMapping { Instrument = Instrument.Square, RangeStart = GMInst.SopranoSax, RangeEnd = GMInst.Clarinet },
            new InstrumentMapping { Instrument = Instrument.Celesta, RangeStart = GMInst.Piccolo, RangeEnd = GMInst.Ocarina },
            new InstrumentMapping { Instrument = Instrument.Square, RangeStart = GMInst.LeadSquare, RangeEnd = GMInst.LeadSquare },
            new InstrumentMapping { Instrument = Instrument.Sawtooth, RangeStart = GMInst.LeadSawtooth, RangeEnd = GMInst.LeadSawtooth },
            new InstrumentMapping { Instrument = Instrument.Square, RangeStart = GMInst.LeadCharang, RangeEnd = GMInst.LeadCharang },
            new InstrumentMapping { Instrument = Instrument.Celesta, RangeStart = GMInst.LeadVoice, RangeEnd = GMInst.LeadVoice },
            new InstrumentMapping { Instrument = Instrument.Sawtooth, RangeStart = GMInst.PadPolysynth, RangeEnd = GMInst.PadPolysynth },
            new InstrumentMapping { Instrument = Instrument.SteelDrum, RangeStart = GMInst.FXRain, RangeEnd = GMInst.FXRain },
            new InstrumentMapping { Instrument = Instrument.SteelDrum, RangeStart = GMInst.TinkleBell, RangeEnd = GMInst.SynthDrum },
            new InstrumentMapping { Instrument = Instrument.Drumkit, RangeStart = GMInst.ReverseCymbal, RangeEnd = GMInst.ReverseCymbal }
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

        private static Dictionary<int, Instrument> CreateInstrumentMap(params InstrumentMapping[] mappings)
        {
            return mappings.SelectMany(mapping => Enumerable.Range(mapping.RangeStart, mapping.RangeEnd - mapping.RangeStart + 1)
                .Select(channel => (channel, mapping.Instrument)))
                .ToDictionary(entry => entry.channel, entry => entry.Instrument);
        }

        public static Song ReadSong(string midiFile, bool debug = false, Dictionary<Instrument, int> instrumentOffsets = null, double masterVolume = 1, Dictionary<Instrument, double> instrumentVolumes = null, bool loop = false, string name = null, int? addressIndex = null)
        {
            var midiEventStream = debug ? new MemoryStream() : null;
            var midiEventWriter = debug ? new StreamWriter(midiEventStream) : null;

            if (midiEventWriter != null)
            {
                midiEventWriter.WriteLine(midiFile);
            }

            var (midiNotes, totalPlayTime) = ReadMidiFile(midiFile);

            if (instrumentOffsets == null)
            {
                const int maxPossibleOctave = 12; // This is to ensure that we don't have a negative number before calculating the octave, which would throw off the result

                instrumentOffsets = midiNotes
                    .Where(note => note.Instrument is not Instrument.Unknown and not Instrument.Drumkit)
                    .GroupBy(note => note.Instrument, note => note.RelativeNoteNumber, (instrument, noteNumbers) =>
                    {
                        var octaves = noteNumbers.GroupBy(relativeNoteNumber => (relativeNoteNumber - 1 + maxPossibleOctave * 12) / 12 - maxPossibleOctave, (octaveNumber, groupedNoteNumbers) => (OctaveNumber: octaveNumber, NoteCount: groupedNoteNumbers.Count()));
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

                midiEventWriter.WriteLine($"Calculated instrument offsets: {string.Join(", ", instrumentOffsets.OrderBy(entry => entry.Key).Select(entry => $"{entry.Key}: {entry.Value}"))}");
            }

            var instrumentsNotMapped = midiNotes.Where(note => note.Instrument == Instrument.Unknown).GroupBy(note => note.OriginalInstrumentName, (originalInstrumentName, notes) => originalInstrumentName).ToList();
            if (instrumentsNotMapped.Count > 0)
            {
                midiEventWriter.WriteLine($"Instruments not mapped: {string.Join(", ", instrumentsNotMapped)}");
            }

            var lastTime = TimeSpan.Zero;
            var currentNotes = new List<Note>();
            var noteGroups = new List<NoteGroup>();

            foreach (var midiNote in midiNotes)
            {
                var instrument = midiNote.Instrument;
                var isInstrumentMapped = instrument != Instrument.Unknown;
                var isNoteInRange = true;
                var effectiveNoteNumber = 0;

                if (isInstrumentMapped)
                {
                    var baseNoteOffset = BaseInstrumentOffsets.TryGetValue(instrument, out var baseOffsetValue) ? baseOffsetValue : 0;
                    var noteOffset = instrumentOffsets != null && instrumentOffsets.TryGetValue(instrument, out var offsetValue) ? offsetValue : 0;
                    var instrumentVolume = instrumentVolumes != null && instrumentVolumes.TryGetValue(instrument, out var instrumentVolumeValue) ? instrumentVolumeValue : 1;
                    var timeDelta = midiNote.CurrentTime - lastTime;

                    effectiveNoteNumber = midiNote.RelativeNoteNumber + noteOffset;
                    isNoteInRange = effectiveNoteNumber > 0 && effectiveNoteNumber <= 48;

                    if (isNoteInRange)
                    {
                        if (timeDelta >= TimeSpan.FromMilliseconds(17) || currentNotes.Count >= ChannelCount) // 17 is 1000ms / 60fps, rounded up
                        {
                            noteGroups.Add(new NoteGroup { Notes = currentNotes, Length = 4 / timeDelta.TotalMinutes, BeatsPerMinute = 1 });
                            currentNotes = new List<Note>();
                            lastTime = midiNote.CurrentTime;
                        }

                        var volume = Math.Min(midiNote.Volume * instrumentVolume * masterVolume, 1);
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
                                Pitch = effectiveNoteNumber - (instrument != Instrument.Drumkit ? baseNoteOffset + 40 : 0),
                                Volume = volume
                            });
                        }
                    }
                }

                if (midiEventWriter != null)
                {
                    var instrumentOrDrum = instrument switch
                    {
                        Instrument.Drumkit => isNoteInRange ? Drums[effectiveNoteNumber] : "Unknown",
                        _ => instrument.ToString()
                    };
                    midiEventWriter.WriteLine($"{lastTime.TotalMilliseconds}: {midiNote.OriginalInstrumentName} {midiNote.OriginalNoteName} volume {midiNote.Volume:F2}{(isInstrumentMapped ? $" => {instrumentOrDrum}" : "")}{(!isNoteInRange ? " (note not in range)" : "")}{(!isInstrumentMapped ? " (instrument not mapped)" : "")}");
                }
            }

            if (currentNotes.Count > 0)
            {
                var currentTime = totalPlayTime;
                var timeDelta = currentTime - lastTime;

                noteGroups.Add(new NoteGroup { Notes = currentNotes, Length = 4 / timeDelta.TotalMinutes, BeatsPerMinute = 1 });
            }

            if (midiEventWriter != null)
            {
                midiEventWriter.WriteLine();
            }

            return new Song
            {
                Name = name,
                AddressIndex = addressIndex,
                NoteGroups = noteGroups,
                Loop = loop,
                DebugStream = midiEventStream
            };
        }

        private static (List<MidiNote> MidiNotes, TimeSpan TotalPlayTime) ReadMidiFile(string midiFile)
        {
            using var fileReader = File.OpenRead(midiFile);
            var music = SmfTrackMerger.Merge(MidiMusic.Read(fileReader));
            var machine = new MidiMachine();

            var currentTimeTicks = 0;
            var notes = new List<MidiNote>();

            foreach (var midiMessage in music.Tracks[0].Messages)
            {
                currentTimeTicks += midiMessage.DeltaTime;
                var currentTime = TimeSpan.FromMilliseconds(music.GetTimePositionInMillisecondsForTick(currentTimeTicks));

                var midiEvent = midiMessage.Event;
                var channel = machine.Channels[midiEvent.Channel];

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
                        var instrumentName = isPercussion ? GeneralMidi.DrumKitsGM2[channel.Program] : GeneralMidi.InstrumentNames[channel.Program];
                        var noteName = isPercussion
                            ? DrumNames.TryGetValue(noteNumber, out var drumName) ? drumName : noteNumber.ToString()
                            : $"{Notes[noteNumber % Notes.Count]}{noteNumber / Notes.Count - 1}";


                        if (instrument != Instrument.Unknown)
                        {
                            var baseNoteOffset = BaseInstrumentOffsets.TryGetValue(instrument, out var baseOffsetValue) ? baseOffsetValue : 0;

                            var relativeNoteNumber = isPercussion
                                ? DrumMap.TryGetValue(noteNumber, out var drum) ? (int)drum : 0
                                : instrument == Instrument.Drumkit
                                    ? (int)Drum.ReverseCymbal
                                    : noteNumber + baseNoteOffset;

                            notes.Add(new()
                            {
                                OriginalInstrumentName = instrumentName,
                                OriginalNoteName = noteName,
                                Instrument = instrument,
                                RelativeNoteNumber = relativeNoteNumber,
                                Volume = velocity / 127d,
                                CurrentTime = currentTime
                            });
                        }
                        else
                        {
                            notes.Add(new()
                            {
                                OriginalInstrumentName = instrumentName,
                                Instrument = instrument
                            });
                        }
                    }
                }
            }

            var totalPlayTime = TimeSpan.FromMilliseconds(music.GetTotalPlayTimeMilliseconds());

            return (notes, totalPlayTime);
        }

        private class MidiNote
        {
            public string OriginalInstrumentName { get; init; }
            public string OriginalNoteName { get; init; }
            public Instrument Instrument { get; init; }
            public int RelativeNoteNumber { get; init; }
            public double Volume { get; init; }
            public TimeSpan CurrentTime { get; init; }
        }
    }
}
