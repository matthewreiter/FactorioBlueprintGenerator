using Commons.Music.Midi;
using MusicBoxCompiler.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GMInst = Commons.Music.Midi.GeneralMidi.Instruments;
using Percussions = Commons.Music.Midi.GeneralMidi.Percussions;

namespace MusicBoxCompiler
{
    public static class MidiReader
    {
        private const int ChannelCount = 10;
        private const int PercussionMidiChannel = 9;
        private static readonly List<string> Notes = new List<string> { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private static readonly Dictionary<byte, string> DrumNames = typeof(Percussions).GetFields(BindingFlags.Public | BindingFlags.Static).ToDictionary(field => (byte)field.GetValue(null), field => field.Name);
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
            new InstrumentMapping { Instrument = Instrument.Celesta, RangeStart = GMInst.LeadVoice, RangeEnd = GMInst.LeadVoice },
            new InstrumentMapping { Instrument = Instrument.SteelDrum, RangeStart = GMInst.TinkleBell, RangeEnd = GMInst.SynthDrum },
            new InstrumentMapping { Instrument = Instrument.Drumkit, RangeStart = GMInst.ReverseCymbal, RangeEnd = GMInst.ReverseCymbal }
        );
        private static readonly Dictionary<int, Drum> DrumMap = new Dictionary<int, Drum>
        {
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
            { Percussions.RideCymbal2, Drum.HiHat1 },
            { Percussions.HiBongo, Drum.Kick2 },
            { Percussions.LowBongo, Drum.Kick1 },
            { Percussions.MuteHiConga, Drum.Snare2 },
            { Percussions.OpenHiConga, Drum.Snare2 },
            { Percussions.LowConga, Drum.Snare1 },
            { Percussions.Cabasa, Drum.Shaker },
            { Percussions.Maracas, Drum.Shaker },
            { Percussions.HiWoodBlock, Drum.Clap },
            { Percussions.LowWoodBlock, Drum.Clap },
            { Percussions.MuteTriangle, Drum.Triangle },
            { Percussions.OpenTriangle, Drum.Triangle },
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

        public static Song ReadSong(string midiFile, StreamWriter midiEventWriter, Dictionary<Instrument, int> instrumentOffsets = null, double masterVolume = 1, Dictionary<Instrument, double> instrumentVolumes = null, bool loop = false, string name = null)
        {
            using var fileReader = File.OpenRead(midiFile);
            var music = SmfTrackMerger.Merge(MidiMusic.Read(fileReader));
            var machine = new MidiMachine();

            var currentTimeTicks = 0;
            var lastTime = TimeSpan.Zero;
            var currentNotes = new List<Note>();
            var noteGroups = new List<NoteGroup>();

            if (midiEventWriter != null)
            {
                midiEventWriter.WriteLine(midiFile);
            }

            foreach (var midiMessage in music.Tracks[0].Messages)
            {
                currentTimeTicks += midiMessage.DeltaTime;
                var currentTime = TimeSpan.FromMilliseconds(music.GetTimePositionInMillisecondsForTick(currentTimeTicks));

                var midiEvent = midiMessage.Event;
                var channel = machine.Channels[midiEvent.Channel];

                machine.ProcessEvent(midiEvent);

                if (midiEvent.EventType == MidiEvent.NoteOn)
                {
                    var noteNumber = midiEvent.Msb;
                    var velocity = midiEvent.Lsb;
                    var isPercussion = midiEvent.Channel == PercussionMidiChannel;
                    var instrument = isPercussion
                        ? Instrument.Drumkit
                        : InstrumentMap.TryGetValue(channel.Program, out var instrumentValue) ? instrumentValue : Instrument.Unknown;
                    var isInstrumentMapped = instrument != Instrument.Unknown;
                    var isNoteInRange = true;

                    if (velocity > 0)
                    {
                        if (isInstrumentMapped)
                        {
                            var baseNoteOffset = BaseInstrumentOffsets.TryGetValue(instrument, out var baseOffsetValue) ? baseOffsetValue : 0;
                            var noteOffset = instrumentOffsets != null && instrumentOffsets.TryGetValue(instrument, out var offsetValue) ? offsetValue : 0;
                            var instrumentVolume = instrumentVolumes != null && instrumentVolumes.TryGetValue(instrument, out var instrumentVolumeValue) ? instrumentVolumeValue : 1;
                            var timeDelta = currentTime - lastTime;

                            var effectiveNoteNumber = isPercussion
                                ? DrumMap.TryGetValue(noteNumber, out var drum) ? (int)drum : 0
                                : instrument == Instrument.Drumkit
                                    ? (int)Drum.ReverseCymbal
                                    : noteNumber + baseNoteOffset + noteOffset;
                            isNoteInRange = effectiveNoteNumber > 0 && effectiveNoteNumber <= 48;

                            if (isNoteInRange)
                            {
                                if (timeDelta >= TimeSpan.FromMilliseconds(17) || currentNotes.Count >= ChannelCount) // 17 is 1000ms / 60fps, rounded up
                                {
                                    noteGroups.Add(new NoteGroup { Notes = currentNotes, Length = 4 / timeDelta.TotalMinutes, BeatsPerMinute = 1 });
                                    currentNotes = new List<Note>();
                                    lastTime = currentTime;
                                }

                                var volume = Math.Min(velocity / 127d * instrumentVolume * masterVolume, 1);
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
                                        Pitch = effectiveNoteNumber,
                                        Volume = volume
                                    });
                                }
                            }
                        }

                        if (midiEventWriter != null)
                        {
                            var instrumentName = isPercussion ? GeneralMidi.DrumKitsGM2[channel.Program] : GeneralMidi.InstrumentNames[channel.Program];
                            var note = isPercussion
                                ? DrumNames.TryGetValue(noteNumber, out var drumName) ? drumName : noteNumber.ToString()
                                : $"{Notes[noteNumber % Notes.Count]}{noteNumber / Notes.Count - 1}";
                            midiEventWriter.WriteLine($"{lastTime.TotalMilliseconds}: {instrumentName} {note} velocity {velocity}{(!isNoteInRange ? " (note not in range)" : "")}{(!isInstrumentMapped ? " (instrument not mapped)" : "")}");
                        }
                    }
                }
            }

            if (currentNotes.Count > 0)
            {
                var currentTime = TimeSpan.FromMilliseconds(music.GetTotalPlayTimeMilliseconds());
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
                NoteGroups = noteGroups,
                Loop = loop
            };
        }
    }
}
