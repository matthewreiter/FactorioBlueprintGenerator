using System;
using System.Collections.Generic;

namespace MusicBoxCompiler.Models;

public class NoteGroup
{
    public List<Note> Notes { get; set; }
    public TimeSpan Length { get; set; }
}
