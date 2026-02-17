using System;
using BlueprintGenerator.Constants;

namespace MusicBoxCompiler.Models;

public class Note
{
    public Instrument Instrument { get; set; }

    /// <summary>
    /// Indicates the pitch of the note, from 1 to 48 (36 for some instruments).
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// The name of the note. Spreadsheets also support the special value "Tempo".
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The volume of the note, from 0.0 to 1.0.
    /// </summary>
    public double Volume { get; set; }

    /// <summary>
    /// When the note starts relative to the beginning of the song.
    /// </summary>
    public TimeSpan StartTime { get; set; }

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

    /// <summary>
    /// Whether this note can participate in deduplication.
    /// </summary>
    public bool CanDeduplicate { get; set; }
}
