using Commons.Music.Midi;
using MusicBoxCompiler.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using GMInst = Commons.Music.Midi.GeneralMidi.Instruments;

namespace MusicBoxCompiler
{
    public static class MidiReader
    {
        private static readonly List<string> Notes = new List<string> { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private static readonly Dictionary<int, Instrument> InstrumentMap = CreateInstrumentMap(
            new InstrumentMapping { Instrument = Instrument.Piano, RangeStart = GMInst.AcousticGrandPiano, RangeEnd = GMInst.Clavi },
            new InstrumentMapping { Instrument = Instrument.Celesta, RangeStart = GMInst.Celesta, RangeEnd = GMInst.MusicBox },
            new InstrumentMapping { Instrument = Instrument.Vibraphone, RangeStart = GMInst.Vibraphone, RangeEnd = GMInst.Dulcimer },
            new InstrumentMapping { Instrument = Instrument.LeadGuitar, RangeStart = GMInst.AcousticGuitarNylon, RangeEnd = GMInst.Guitarharmonics },
            new InstrumentMapping { Instrument = Instrument.BassGuitar, RangeStart = GMInst.ElectricBassFinger, RangeEnd = GMInst.SynthBass2 },
            new InstrumentMapping { Instrument = Instrument.PluckedStrings, RangeStart = GMInst.Violin, RangeEnd = GMInst.SynthStrings2 },
            new InstrumentMapping { Instrument = Instrument.Square, RangeStart = GMInst.LeadSquare, RangeEnd = GMInst.LeadSquare },
            new InstrumentMapping { Instrument = Instrument.Sawtooth, RangeStart = GMInst.LeadSawtooth, RangeEnd = GMInst.LeadSawtooth },
            new InstrumentMapping { Instrument = Instrument.SteelDrum, RangeStart = GMInst.SteelDrums, RangeEnd = GMInst.SteelDrums },
            new InstrumentMapping { Instrument = Instrument.Drumkit, RangeStart = GMInst.SynthDrum, RangeEnd = GMInst.SynthDrum }
        );
        private static readonly Dictionary<Instrument, int> BaseInstrumentOffsets = new Dictionary<Instrument, int>
        {
            { Instrument.Drumkit, -34 },
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

        public static List<NoteGroup> ReadSong(string midiFile, StreamWriter midiEventWriter, Dictionary<Instrument, int> instrumentOffsets = null, double volume = 1)
        {
            using var fileReader = File.OpenRead(midiFile);
            var music = MidiMusic.Read(fileReader);

            var timeManager = new TimeManager();
            var player = new MidiPlayer(music, timeManager);
            var machine = new MidiMachine();

            var lastTime = TimeSpan.Zero;
            var currentNotes = new List<Note>();
            var noteGroups = new List<NoteGroup>();

            if (midiEventWriter != null)
            {
                midiEventWriter.WriteLine(midiFile);
            }

            player.EventReceived += midiEvent =>
            {
                var channel = machine.Channels[midiEvent.Channel];

                machine.ProcessEvent(midiEvent);

                if (midiEvent.EventType == MidiEvent.NoteOn)
                {
                    var currentTime = player.PositionInTime;

                    if (InstrumentMap.TryGetValue(channel.Program, out var instrument))
                    {
                        var noteNumber = midiEvent.Msb;
                        var velocity = midiEvent.Lsb;
                        var baseNoteOffset = BaseInstrumentOffsets.TryGetValue(instrument, out var baseOffsetValue) ? baseOffsetValue : 0;
                        var noteOffset = instrumentOffsets != null && instrumentOffsets.TryGetValue(instrument, out var offsetValue) ? offsetValue : 0;
                        var timeDelta = currentTime - lastTime;

                        var effectiveNoteNumber = noteNumber + baseNoteOffset + noteOffset;
                        var isNoteInRange = effectiveNoteNumber > 0 && effectiveNoteNumber <= 48;

                        if (isNoteInRange && velocity > 0)
                        {
                            if (timeDelta < TimeSpan.FromMilliseconds(17)) // 17 is 1000ms / 60fps, rounded up
                            {
                                timeDelta = TimeSpan.Zero;
                            }
                            else
                            {
                                noteGroups.Add(new NoteGroup { Notes = currentNotes, Length = 4 / timeDelta.TotalMinutes, BeatsPerMinute = 1 });
                                currentNotes = new List<Note>();
                                lastTime = currentTime;
                            }

                            currentNotes.Add(new Note
                            {
                                Instrument = instrument,
                                Number = effectiveNoteNumber,
                                Pitch = effectiveNoteNumber,
                                Volume = Math.Min(velocity / 127d * volume, 1)
                            });
                        }

                        if (midiEventWriter != null)
                        {
                            midiEventWriter.WriteLine($"{lastTime.TotalMilliseconds}: {GeneralMidi.InstrumentNames[channel.Program]} {Notes[noteNumber % Notes.Count]}{noteNumber / Notes.Count - 1}{(!isNoteInRange ? " (note not in range)" : "")}");
                        }
                    }
                }
            };

            var doneEvent = new AutoResetEvent(false);
            player.Finished += () =>
            {
                doneEvent.Set();
            };

            player.Play();
            doneEvent.WaitOne();

            if (midiEventWriter != null)
            {
                midiEventWriter.WriteLine();
            }

            return noteGroups;
        }

        private class TimeManager : IMidiPlayerTimeManager
        {
            public void WaitBy(int addedMilliseconds) { }
        }
    }
}
