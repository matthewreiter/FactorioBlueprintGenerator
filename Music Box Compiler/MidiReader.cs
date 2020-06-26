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
            new InstrumentMapping { Instrument = Instrument.LeadGuitar, RangeStart = GMInst.AcousticGuitarNylon, RangeEnd = GMInst.Guitarharmonics },
            new InstrumentMapping { Instrument = Instrument.BassGuitar, RangeStart = GMInst.ElectricBassFinger, RangeEnd = GMInst.SynthBass2 },
            new InstrumentMapping { Instrument = Instrument.Drum, RangeStart = GMInst.SynthDrum, RangeEnd = GMInst.SynthDrum },
            new InstrumentMapping { Instrument = Instrument.SteelDrum, RangeStart = GMInst.SteelDrums, RangeEnd = GMInst.SteelDrums },
            new InstrumentMapping { Instrument = Instrument.Celesta, RangeStart = GMInst.Celesta, RangeEnd = GMInst.Dulcimer }
        );
        private static readonly Dictionary<Instrument, int> BaseInstrumentOffsets = new Dictionary<Instrument, int>
        {
            { Instrument.Piano, -52 },
            { Instrument.LeadGuitar, -40 },
            { Instrument.BassGuitar, -40 },
            { Instrument.Drum, -34 },
            { Instrument.SteelDrum, -52 },
            { Instrument.Celesta, -76 }
        };

        private static Dictionary<int, Instrument> CreateInstrumentMap(params InstrumentMapping[] mappings)
        {
            return mappings.SelectMany(mapping => Enumerable.Range(mapping.RangeStart, mapping.RangeEnd - mapping.RangeStart + 1)
                .Select(channel => (channel, mapping.Instrument)))
                .ToDictionary(entry => entry.channel, entry => entry.Instrument);
        }

        public static List<NoteGroup> ReadSong(string midiFile, StreamWriter midiEventWriter, Dictionary<Instrument, int> instrumentOffsets = null)
        {
            using var fileReader = File.OpenRead(midiFile);
            var music = MidiMusic.Read(fileReader);

            var timeManager = new TimeManager();
            var player = new MidiPlayer(music, timeManager);

            var lastTime = TimeSpan.Zero;
            var currentNotes = new List<Note>();
            var noteGroups = new List<NoteGroup>();

            if (midiEventWriter != null)
            {
                midiEventWriter.WriteLine(midiFile);
            }

            player.EventReceived += midiEvent =>
            {
                if (midiEvent.EventType == MidiEvent.NoteOn)
                {
                    var currentTime = player.PositionInTime;
                    var channel = midiEvent.Channel;

                    if (InstrumentMap.TryGetValue(channel, out var instrument))
                    {
                        var noteNumber = midiEvent.Msb;
                        var baseNoteOffset = BaseInstrumentOffsets.TryGetValue(instrument, out var baseOffsetValue) ? baseOffsetValue : 0;
                        var noteOffset = instrumentOffsets != null && instrumentOffsets.TryGetValue(instrument, out var offsetValue) ? offsetValue : 0;
                        var timeDelta = currentTime - lastTime;

                        var effectiveNoteNumber = noteNumber + baseNoteOffset + noteOffset;
                        var isNoteInRange = effectiveNoteNumber > 0 && effectiveNoteNumber <= 48;

                        if (isNoteInRange)
                        {
                            if (timeDelta < TimeSpan.FromMilliseconds(80))
                            {
                                timeDelta = TimeSpan.Zero;
                            }
                            else
                            {
                                noteGroups.Add(new NoteGroup { Notes = currentNotes, Length = 4 / timeDelta.TotalMinutes, BeatsPerMinute = 1 });
                                currentNotes = new List<Note>();
                                lastTime = currentTime;
                            }

                            currentNotes.Add(new Note { Instrument = Instrument.Piano, Number = effectiveNoteNumber, Pitch = effectiveNoteNumber });
                        }

                        if (midiEventWriter != null)
                        {
                            midiEventWriter.WriteLine($"{lastTime.TotalMilliseconds}: {GeneralMidi.InstrumentNames[channel]} {Notes[noteNumber % Notes.Count]}{noteNumber / Notes.Count - 1}{(!isNoteInRange ? " (note not in range)" : "")}");
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
