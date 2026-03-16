using System;
using System.Collections.Generic;

namespace MusicBoxCompiler.Models;

public class NoteGroup
{
    public List<Note> Notes { get; set; }
    public string Lyrics { get; set; }
    public bool IsStartOfLine { get; set; }
    public TimeSpan Length { get; set; }
}
