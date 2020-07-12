using System.Collections.Generic;

namespace MusicBoxCompiler.Models
{
    public class Song
    {
        public List<NoteGroup> NoteGroups { get; set; }
        public bool Loop { get; set; }
    }
}
