using Commons.Music.Midi;
using MusicBoxCompiler.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using static MusicBoxCompiler.Constants;
using GMInst = Commons.Music.Midi.GeneralMidi.Instruments;
using Percussions = Commons.Music.Midi.GeneralMidi.Percussions;

namespace MusicBoxCompiler;

public static class MidiReader
{
    // https://web.archive.org/web/20250511231913/http://www.music.mcgill.ca/~ich/classes/mumt306/StandardMIDIfileformat.html
    private const int PercussionMidiChannel = 9;
    private static readonly List<string> Notes = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
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
        new InstrumentMapping { Instrument = Instrument.PluckedStrings, RangeStart = GMInst.Violin, RangeEnd = GMInst.Cello },
        new InstrumentMapping { Instrument = Instrument.BassGuitar, RangeStart = GMInst.Contrabass, RangeEnd = GMInst.Contrabass },
        new InstrumentMapping { Instrument = Instrument.PluckedStrings, RangeStart = GMInst.TremoloStrings, RangeEnd = GMInst.SynthStrings2 },
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
        new InstrumentMapping { Instrument = Instrument.SteelDrum, RangeStart = GMInst.PadSweep, RangeEnd = GMInst.FXScifi },
        new InstrumentMapping { Instrument = Instrument.LeadGuitar, RangeStart = GMInst.Sitar, RangeEnd = GMInst.Banjo },
        new InstrumentMapping { Instrument = Instrument.SteelDrum, RangeStart = GMInst.Kalimba, RangeEnd = GMInst.Kalimba },
        new InstrumentMapping { Instrument = Instrument.Square, RangeStart = GMInst.Shanai, RangeEnd = GMInst.Shanai },
        new InstrumentMapping { Instrument = Instrument.SteelDrum, RangeStart = GMInst.TinkleBell, RangeEnd = GMInst.SynthDrum },
        new InstrumentMapping { Instrument = Instrument.Drumkit, RangeStart = GMInst.ReverseCymbal, RangeEnd = GMInst.ReverseCymbal },
        new InstrumentMapping { Instrument = Instrument.BassGuitar, RangeStart = GMInst.GuitarFretNoise, RangeEnd = GMInst.GuitarFretNoise },
        new InstrumentMapping { Instrument = Instrument.SteelDrum, RangeStart = GMInst.Seashore, RangeEnd = GMInst.Seashore },
        new InstrumentMapping { Instrument = Instrument.BassGuitar, RangeStart = GMInst.Applause, RangeEnd = GMInst.Applause }
    );
    private static readonly Dictionary<int, Drum> DrumMap = new()
    {
        [PercussionsGM2.HighQ] = Drum.HighQ,
        [PercussionsGM2.Slap] = Drum.Clap,
        [PercussionsGM2.ScratchPush] = Drum.Fx,
        [PercussionsGM2.ScratchPull] = Drum.Fx,
        [PercussionsGM2.MetronomeClick] = Drum.Triangle,
        [PercussionsGM2.MetronomeBell] = Drum.Cowbell,
        [Percussions.AcousticBassDrum] = Drum.Kick1,
        [Percussions.BassDrum1] = Drum.Kick2,
        [Percussions.SideStick] = Drum.Percussion1,
        [Percussions.AcousticSnare] = Drum.Snare1,
        [Percussions.ElectricSnare] = Drum.Snare2,
        [Percussions.HandClap] = Drum.Clap,
        [Percussions.LowFloorTom] = Drum.Snare1,
        [Percussions.ClosedHiHat] = Drum.HiHat2,
        [Percussions.HighFloorTom] = Drum.Snare1,
        [Percussions.PedalHiHat] = Drum.HiHat2,
        [Percussions.LowTom] = Drum.Snare3,
        [Percussions.OpenHiHat] = Drum.HiHat2,
        [Percussions.LowMidTom] = Drum.Snare2,
        [Percussions.HiMidTom] = Drum.Snare2,
        [Percussions.CrashCymbal1] = Drum.Crash,
        [Percussions.HighTom] = Drum.Snare3,
        [Percussions.RideCymbal1] = Drum.HiHat1,
        [Percussions.ChineseCymbal] = Drum.HiHat1,
        [Percussions.RideBell] = Drum.Cowbell,
        [Percussions.Tambourine] = Drum.Shaker,
        [Percussions.SplashCymbal] = Drum.HiHat1,
        [Percussions.Cowbell] = Drum.Cowbell,
        [Percussions.CrashCymbal2] = Drum.Crash,
        [Percussions.Vibraslap] = Drum.Clap,
        [Percussions.RideCymbal2] = Drum.HiHat1,
        [Percussions.HiBongo] = Drum.Kick2,
        [Percussions.LowBongo] = Drum.Kick1,
        [Percussions.MuteHiConga] = Drum.Snare2,
        [Percussions.OpenHiConga] = Drum.Snare2,
        [Percussions.LowConga] = Drum.Snare1,
        [Percussions.HighAgogo] = Drum.Cowbell,
        [Percussions.LowAgogo] = Drum.Cowbell,
        [Percussions.Cabasa] = Drum.Shaker,
        [Percussions.Maracas] = Drum.Shaker,
        [Percussions.ShortWhistle] = Drum.Fx,
        [Percussions.LongWhistle] = Drum.Fx,
        [Percussions.HiWoodBlock] = Drum.Clap,
        [Percussions.LowWoodBlock] = Drum.Clap,
        [Percussions.MuteTriangle] = Drum.Triangle,
        [Percussions.OpenTriangle] = Drum.Triangle,
        [PercussionsGM2.Shaker] = Drum.Shaker,
        [PercussionsGM2.JingleBell] = Drum.Shaker,
        [PercussionsGM2.Belltree] = Drum.Cowbell,
        [87] = Drum.Percussion1,
        [88] = Drum.Percussion1,
        [89] = Drum.Percussion1,
        [91] = Drum.Percussion1,
    };
    private static readonly Dictionary<Instrument, InstrumentInfo> Instruments = new()
    {
        [Instrument.Drumkit] = new(0, 17),
        [Instrument.Piano] = new(-52, 48),
        [Instrument.BassGuitar] = new(-40, 36),
        [Instrument.LeadGuitar] = new(-40, 36),
        [Instrument.Sawtooth] = new(-40, 36, 0.75),
        [Instrument.Square] = new(-40, 36, 0.5),
        [Instrument.Celesta] = new(-76, 36),
        [Instrument.Vibraphone] = new(-76, 36),
        [Instrument.PluckedStrings] = new(-64, 36),
        [Instrument.SteelDrum] = new(-52, 36),
    };
    private const Instrument LowFallbackInstrument = Instrument.LeadGuitar;
    private const Instrument HighFallbackInstrument = Instrument.Celesta;
    private static readonly TimeSpan TickDuration = TimeSpan.FromMilliseconds(1000d / 60); // 1000ms / 60fps (approximately 17 ms per tick)

    private static Dictionary<int, Instrument> CreateInstrumentMap(params InstrumentMapping[] mappings)
    {
        return mappings.SelectMany(mapping => Enumerable.Range(mapping.RangeStart, mapping.RangeEnd - mapping.RangeStart + 1)
            .Select(channel => (channel, mapping.Instrument)))
            .ToDictionary(entry => entry.channel, entry => entry.Instrument);
    }

    public static Song ReadSong(string midiFile, bool debug, Dictionary<Instrument, int> instrumentOffsets, double masterVolume, Dictionary<Instrument, double> instrumentVolumes, bool allowInstrumentFallback, bool expandNotes, int? channelCount)
    {
        const int unreasonablyHighOctave = 12; // This is to ensure that we don't have a negative number before calculating the octave, which would throw off the result

        var midiEventStream = debug ? new MemoryStream() : null;
        var midiEventWriter = debug ? new StreamWriter(midiEventStream) : null;

        midiEventWriter?.WriteLine(midiFile);

        var midiData = ReadMidiFile(midiFile);
        var midiNotes = midiData.Notes;
        var totalPlayTime = midiData.TotalPlayTime;
        var trackName = midiData.TrackName;
        var text = midiData.Text;
        var copyright = midiData.Copyright;

        if (midiEventWriter != null)
        {
            midiEventWriter.WriteLine($"Total play time: {totalPlayTime}");

            if (trackName != null)
            {
                midiEventWriter.WriteLine($"Track name: {trackName}");
            }

            if (text != null)
            {
                midiEventWriter.WriteLine($"Text: {text}");
            }

            if (copyright != null)
            {
                midiEventWriter.WriteLine($"Copyright: {copyright}");
            }
        }

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
                    var maxAllowedOctave = Instruments[instrument].NoteCount / 12 - 1;

                    const double newNotesOutOfRangePenalty = 4;
                    const double octaveShiftPenaltyBase = 0.05;
                    const double octaveShiftPenaltyGrowth = 2;

                    return (
                        Instrument: instrument,
                        NoteShift: minOctaveShift < 0 || maxOctaveShift > 0
                            ? Enumerable.Range(minOctaveShift, maxOctaveShift - minOctaveShift + 1)
                                .Select(octavesOutOfRange =>
                                {
                                    bool IsOutOfRange(int octaveNumber) => octaveNumber < -1 || octaveNumber > maxAllowedOctave;

                                    var octavesThatAreOutOfRange = octaves.Where(octave => IsOutOfRange(octave.OctaveNumber - octavesOutOfRange));

                                    return (
                                        OctaveShift: -octavesOutOfRange,
                                        NotesStillOutOfRange: octavesThatAreOutOfRange.Where(octave => IsOutOfRange(octave.OctaveNumber)).Sum(octave => octave.NoteCount),
                                        NewNotesOutOfRange: octavesThatAreOutOfRange.Where(octave => !IsOutOfRange(octave.OctaveNumber)).Sum(octave => octave.NoteCount)
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

        IEnumerable<MidiNote> expandedMidiNotes = expandNotes
            ? midiNotes.SelectMany(ExpandNote).OrderBy(midiNote => midiNote.StartTime)
            : midiNotes;

        var lastTime = TimeSpan.Zero;
        var currentTime = TimeSpan.Zero;
        var currentNotes = new List<Note>();
        var noteGroups = new List<NoteGroup>();

        foreach (var midiNote in expandedMidiNotes)
        {
            var instrument = midiNote.Instrument;
            var isNoteInRange = true;
            var effectiveNoteNumber = 0;
            var volume = 0d;

            if (Instruments.TryGetValue(instrument, out var instrumentInfo))
            {
                var noteOffset = instrumentOffsets != null && instrumentOffsets.TryGetValue(instrument, out var offsetValue) ? offsetValue : 0;
                var instrumentVolume = instrumentVolumes != null && instrumentVolumes.TryGetValue(instrument, out var instrumentVolumeValue) ? instrumentVolumeValue : 1;

                effectiveNoteNumber = midiNote.RelativeNoteNumber + noteOffset;
                isNoteInRange = effectiveNoteNumber > (instrument == Instrument.Drumkit ? 0 : -12) && effectiveNoteNumber <= instrumentInfo.NoteCount;
                volume = Math.Min(midiNote.Velocity * midiNote.Expression * midiNote.ChannelVolume * instrumentInfo.BaseVolume * instrumentVolume * masterVolume, 1);

                if (!isNoteInRange && instrument != Instrument.Drumkit)
                {
                    if (allowInstrumentFallback)
                    {
                        var originalBaseNoteOffset = instrumentInfo.BaseNoteOffset;

                        instrument = effectiveNoteNumber <= 0 ? LowFallbackInstrument : HighFallbackInstrument;
                        instrumentInfo = Instruments[instrument];
                        effectiveNoteNumber += instrumentInfo.BaseNoteOffset - originalBaseNoteOffset;
                        isNoteInRange = effectiveNoteNumber > -12 && effectiveNoteNumber <= instrumentInfo.NoteCount;
                    }

                    // As a last resort, force notes into range by adjusting their octave up or down
                    if (!isNoteInRange)
                    {
                        if (effectiveNoteNumber < 0)
                        {
                            effectiveNoteNumber %= 12;
                        }
                        else
                        {
                            effectiveNoteNumber = instrumentInfo.NoteCount + effectiveNoteNumber % 12 - 12;
                        }

                        isNoteInRange = true;
                    }
                }

                if (isNoteInRange)
                {
                    // Replace a note that is up to an octave too low with its second through fourth harmonic, which sound close enough to the original note
                    List<int> effectiveNoteNumbers = effectiveNoteNumber > 0
                        ? [effectiveNoteNumber]
                        : [effectiveNoteNumber + 12, effectiveNoteNumber + 19, effectiveNoteNumber + 24];

                    if (midiNote.StartTime - currentTime >= TickDuration || channelCount is not null && currentNotes.Count + effectiveNoteNumbers.Count > channelCount)
                    {
                        noteGroups.Add(new NoteGroup { Notes = currentNotes, Length = currentTime - lastTime });
                        currentNotes = [];
                        lastTime = currentTime;
                        currentTime = midiNote.StartTime;
                    }

                    foreach (var noteNumber in effectiveNoteNumbers)
                    {
                        var duplicateNote = currentNotes.Find(note => note.Instrument == instrument && note.Number == noteNumber);

                        if (duplicateNote is not null)
                        {
                            duplicateNote.Volume = Math.Max(volume, duplicateNote.Volume);
                        }
                        else
                        {
                            currentNotes.Add(new Note
                            {
                                Instrument = instrument,
                                Number = noteNumber,
                                Volume = volume,
                                Duration = midiNote.EndTime.HasValue && !midiNote.IsExpanded ? midiNote.EndTime.Value - midiNote.StartTime : TimeSpan.Zero,
                            });
                        }
                    }
                }
            }

            if (midiEventWriter != null)
            {
                var notePlayed = effectiveNoteNumber - instrumentInfo.BaseNoteOffset;
                var instrumentAndNote = instrument switch
                {
                    Instrument.Drumkit => isNoteInRange ? Drums[effectiveNoteNumber - 1] : "Unknown",
                    _ => $"{instrument} {Notes[(notePlayed + unreasonablyHighOctave * Notes.Count) % Notes.Count]}{notePlayed / Notes.Count - 1}"
                };

                var isInstrumentMapped = instrument != Instrument.Unknown;
                midiEventWriter.WriteLine($"{lastTime:mm\\:ss\\.fff}: {midiNote.OriginalInstrumentName} {midiNote.OriginalNoteName} velocity {midiNote.Velocity:F2} expression {midiNote.Expression:F2} channel volume {midiNote.ChannelVolume:F2}" +
                    (!midiNote.IsExpanded && midiNote.EndTime is not null ? $" duration {midiNote.EndTime - midiNote.StartTime}" : "") +
                    (isInstrumentMapped ? $" => {instrumentAndNote} volume {volume:F2}" : "") +
                    (!isNoteInRange ? " (note not in range)" : "") +
                    (!isInstrumentMapped ? " (instrument not mapped)" : "" +
                    (midiNote.IsExpanded ? " (expanded)" : "")));
            }
        }

        if (currentNotes.Count > 0)
        {
            noteGroups.Add(new() { Notes = currentNotes, Length = totalPlayTime - lastTime });
        }

        midiEventWriter?.WriteLine();
        midiEventWriter?.Flush();

        return new Song
        {
            NoteGroups = noteGroups,
            DebugStream = midiEventStream
        };
    }

    private static IEnumerable<MidiNote> ExpandNote(MidiNote note)
    {
        yield return note;

        if (note.EndTime is null)
        {
            yield break;
        }

        var pressureIndex = 0;
        var pressure = 0d;

        var expressionIndex = 0;
        var expression = note.Expression;

        var volumeIndex = 0;
        var volume = note.ChannelVolume;

        var step = TickDuration * 4;
        for (var time = note.StartTime + step; time < note.EndTime; time += step)
        {
            while (pressureIndex < note.PressuresChanges.Count && note.PressuresChanges[pressureIndex].Time <= time)
            {
                pressure = note.PressuresChanges[pressureIndex].Pressure;
                pressureIndex++;
            }

            while (expressionIndex < note.ExpressionChanges.Count && note.ExpressionChanges[expressionIndex].Time <= time)
            {
                expression = note.ExpressionChanges[expressionIndex].Expression;
                expressionIndex++;
            }

            while (volumeIndex < note.VolumeChanges.Count && note.VolumeChanges[volumeIndex].Time <= time)
            {
                volume = note.VolumeChanges[volumeIndex].Volume;
                volumeIndex++;
            }

            yield return note with
            {
                StartTime = time,
                Velocity = note.Velocity * (pressure + 1) * 0.25,
                Expression = expression,
                ChannelVolume = volume,
                IsExpanded = true
            };
        }
    }

    private static MidiData ReadMidiFile(string midiFile)
    {
        using var fileReader = File.OpenRead(midiFile);
        var music = SmfTrackMerger.Merge(MidiMusic.Read(fileReader));
        var machine = new MidiMachine(); // https://github.com/atsushieno/managed-midi/blob/master/Commons.Music.Midi.Shared/MidiMachine.cs

        var tempo = MidiMetaType.DefaultTempo;
        var currentTime = TimeSpan.Zero;
        var notes = new List<MidiNote>();
        var activeNotes = Enumerable.Range(0, machine.Channels.Count).Select(_ => new Dictionary<(byte Program, byte NoteNumber), MidiNote>()).ToArray();
        var trackName = new List<string>();
        var text = new List<string>();
        var copyright = new List<string>();

        foreach (var midiMessage in music.Tracks[0].Messages)
        {
            var midiEvent = midiMessage.Event;
            var channel = machine.Channels[midiEvent.Channel];
            var currentChannelActiveNotes = activeNotes[midiEvent.Channel];

            currentTime += TimeSpan.FromMilliseconds(tempo / 1000d * midiMessage.DeltaTime / music.DeltaTimeSpec);

            machine.ProcessEvent(midiEvent);

            switch (midiEvent.EventType)
            {
                case MidiEvent.Meta:
                    switch (midiEvent.MetaType)
                    {
                        case MidiMetaType.TrackName:
                            trackName.Add(Encoding.ASCII.GetString(midiEvent.ExtraData));
                            break;
                        case MidiMetaType.Text:
                            text.Add(Encoding.ASCII.GetString(midiEvent.ExtraData));
                            break;
                        case MidiMetaType.Copyright:
                            copyright.Add(Encoding.ASCII.GetString(midiEvent.ExtraData));
                            break;
                        case MidiMetaType.Tempo:
                            tempo = MidiMetaType.GetTempo(midiEvent.ExtraData, midiEvent.ExtraDataOffset);
                            break;
                    }

                    break;
                case MidiEvent.NoteOn or MidiEvent.NoteOff:
                    {
                        var noteNumber = midiEvent.Msb;
                        var velocity = midiEvent.Lsb;

                        if (currentChannelActiveNotes.Remove((channel.Program, noteNumber), out var previousNote))
                        {
                            previousNote.EndTime = currentTime;
                        }

                        if (midiEvent.EventType == MidiEvent.NoteOn && velocity > 0)
                        {
                            var isPercussion = midiEvent.Channel == PercussionMidiChannel;
                            var channelVolume = channel.Controls[MidiCC.Volume];
                            var expression = channel.Controls[MidiCC.Expression];
                            var instrument = isPercussion
                                ? Instrument.Drumkit
                                : InstrumentMap.TryGetValue(channel.Program, out var instrumentValue) ? instrumentValue : Instrument.Piano;
                            var instrumentInfo = Instruments.TryGetValue(instrument, out var info) ? info : null;
                            var instrumentName = (isPercussion ? GeneralMidi.DrumKitsGM2.ElementAtOrDefault(channel.Program) : GeneralMidi.InstrumentNames.ElementAtOrDefault(channel.Program)) ?? channel.Program.ToString();
                            var noteName = isPercussion
                                ? DrumNames.TryGetValue(noteNumber, out var drumName) ? drumName : noteNumber.ToString()
                                : $"{Notes[noteNumber % Notes.Count]}{noteNumber / Notes.Count - 1}";

                            var relativeNoteNumber = instrument == Instrument.Unknown
                                ? 0
                                : isPercussion
                                    ? (int)(DrumMap.TryGetValue(noteNumber, out var drum) ? drum : Drum.Snare1)
                                    : instrument == Instrument.Drumkit
                                        ? (int)Drum.ReverseCymbal
                                        : noteNumber + instrumentInfo.BaseNoteOffset;

                            var note = new MidiNote
                            {
                                OriginalInstrumentName = instrumentName,
                                OriginalNoteName = noteName,
                                OriginalNoteNumber = noteNumber,
                                Instrument = instrument,
                                RelativeNoteNumber = relativeNoteNumber,
                                Velocity = velocity / 127d,
                                Expression = expression > 0 ? expression / 127d : 1,
                                ChannelVolume = channelVolume > 0 ? channelVolume / 127d : 1,
                                StartTime = currentTime
                            };

                            notes.Add(note);
                            currentChannelActiveNotes[(channel.Program, noteNumber)] = note;
                        }
                    }

                    break;
                case MidiEvent.PAf: // Polyphonic aftertouch
                    {
                        var noteNumber = midiEvent.Msb;
                        var pressure = midiEvent.Lsb / 127d;

                        if (currentChannelActiveNotes.TryGetValue((channel.Program, noteNumber), out var activeNote))
                        {
                            activeNote.PressuresChanges.Add((currentTime, pressure));
                        }
                    }

                    break;
                case MidiEvent.MidiStop:
                    foreach (var entry in currentChannelActiveNotes)
                    {
                        entry.Value.EndTime = currentTime;
                    }

                    currentChannelActiveNotes.Clear();

                    break;
                case MidiEvent.CC:
                    switch (midiEvent.Msb)
                    {
                        case MidiCC.AllSoundOff or MidiCC.ResetAllControllers or MidiCC.AllNotesOff or MidiCC.OmniModeOff or MidiCC.OmniModeOn or MidiCC.PolyModeOnOff or MidiCC.PolyModeOn:
                            foreach (var entry in currentChannelActiveNotes)
                            {
                                entry.Value.EndTime = currentTime;
                            }

                            currentChannelActiveNotes.Clear();

                            break;
                        case MidiCC.Expression:
                            var expression = midiEvent.Lsb / 127d;

                            foreach (var entry in currentChannelActiveNotes)
                            {
                                entry.Value.ExpressionChanges.Add((currentTime, expression));
                            }

                            break;
                        case MidiCC.Volume:
                            var channelVolume = midiEvent.Lsb / 127d;

                            foreach (var entry in currentChannelActiveNotes)
                            {
                                entry.Value.VolumeChanges.Add((currentTime, channelVolume));
                            }

                            break;
                    }

                    break;
            }
        }

        // Close out any notes that are still active at the end of the song
        foreach (var channelActiveNotes in activeNotes)
        {
            foreach (var entry in channelActiveNotes)
            {
                entry.Value.EndTime = currentTime;
            }
        }

        return new MidiData
        {
            Notes = [.. notes.Where(note => note.EndTime != note.StartTime)],
            TotalPlayTime = currentTime,
            TrackName = trackName.Count > 0 ? string.Join(", ", trackName) : null,
            Text = text.Count > 0 ? string.Join("", text) : null,
            Copyright = copyright.Count > 0 ? string.Join(", ", copyright) : null
        };
    }

    private record InstrumentInfo(int BaseNoteOffset, int NoteCount, double BaseVolume = 1);

    private record MidiData
    {
        public List<MidiNote> Notes { get; init; }
        public TimeSpan TotalPlayTime { get; init; }
        public string TrackName { get; init; }
        public string Text { get; init; }
        public string Copyright { get; init; }
    }

    private record MidiNote
    {
        public string OriginalInstrumentName { get; init; }
        public string OriginalNoteName { get; init; }
        public int OriginalNoteNumber { get; init; }
        public Instrument Instrument { get; init; }
        public int RelativeNoteNumber { get; init; }
        public double Velocity { get; init; }
        public double Expression { get; init; }
        public double ChannelVolume { get; init; }
        public TimeSpan StartTime { get; init; }
        public TimeSpan? EndTime { get; set; }
        public List<(TimeSpan Time, double Pressure)> PressuresChanges { get; } = [];
        public List<(TimeSpan Time, double Expression)> ExpressionChanges { get; } = [];
        public List<(TimeSpan Time, double Volume)> VolumeChanges { get; } = [];
        public bool IsExpanded { get; init; }
    }
}
