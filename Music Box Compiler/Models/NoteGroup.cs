using System.Collections.Generic;

namespace MusicBoxCompiler.Models
{
    public class NoteGroup
    {
        public List<Note> Notes { get; set; }
        public double Length { get; set; }
        public int? BeatsPerMinute { get; set; }
    }
}
