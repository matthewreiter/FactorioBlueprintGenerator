using System.Collections.Generic;
using System.IO;

namespace MusicBoxCompiler.Models
{
    public class Song
    {
        public string Name { get; set; }
        public int? AddressIndex { get; set; }
        public List<NoteGroup> NoteGroups { get; set; }
        public bool Loop { get; set; }
        public MemoryStream DebugStream { get; set; }
    }
}
