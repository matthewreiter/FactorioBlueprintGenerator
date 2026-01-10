using System;

namespace MusicBoxCompiler.Models;

public class Note
{
    public Instrument Instrument { get; set; }

    /// <summary>
    /// Indicates the pitch of the note, from 1 to 48 (36 for some instruments).
    /// </summary>
    public int Number { get; set; }

    public string Name { get; set; }

    public double Volume { get; set; }

    /// <summary>
    /// The duration of the note from key press to release.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// 1 for a whole note, 2 for a half note, 4 for a quarter note, etc.
    /// </summary>
    public double InverseLength { get; set; }

    /// <summary>
    /// The music box channel on which this note is played.
    /// </summary>
    public int? Channel { get; set; }

    /// <summary>
    /// If not null, represents the previous note change or the parent note if this is the first note change.
    /// </summary>
    public Note PreviousNote { get; set; }
}
