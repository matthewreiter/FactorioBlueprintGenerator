using BlueprintGenerator.Constants;
using MusicBoxCompiler.Models;
using System;

namespace MusicBoxCompiler.Utils;

public static class AudioClipInfo
{
    /// <summary>
    /// Gets the length of the audio clip for the given note.
    /// </summary>
    /// <param name="note">The note for which to get the audio clip length.</param>
    /// <returns>The non-silent length of the audio clip, in ticks (1 tick = 1/60th of a second).</returns>
    /// <exception cref="ArgumentException">If an unsupported instrument or drum is provided.</exception>
    public static int GetAudioClipLength(Note note) =>
        note.Instrument switch
        {
            Instrument.Drumkit => (Drum)note.Number switch
            {
                Drum.Kick1 => 14,
                Drum.Kick2 => 15,
                Drum.Snare1 => 8,
                Drum.Snare2 => 9,
                Drum.Snare3 => 12,
                Drum.HiHat1 => 5,
                Drum.HiHat2 => 5,
                Drum.Fx => 9,
                Drum.HighQ => 3,
                Drum.Percussion1 => 5,
                Drum.Percussion2 => 3,
                Drum.Crash => 68,
                Drum.ReverseCymbal => 82,
                Drum.Clap => 6,
                Drum.Shaker => 15,
                Drum.Cowbell => 15,
                Drum.Triangle => 39,
                _ => throw new ArgumentException($"Unsupported drum: {note.Number}")
            },
            Instrument.Piano => 27,
            Instrument.BassGuitar => 7,
            Instrument.LeadGuitar => 7,
            Instrument.Sawtooth => 7,
            Instrument.Square => 24,
            Instrument.Celesta => 55,
            Instrument.Vibraphone => 31,
            Instrument.PluckedStrings => 34,
            Instrument.SteelDrum => 24,
            _ => throw new ArgumentException($"Unsupported instrument: {note.Instrument}")
        };
}
