using System.Collections.Generic;
using System.IO;

namespace MusicBoxCompiler.Models;

public record Song
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Artist { get; set; }
    public int? AddressIndex { get; set; }
    public List<NoteGroup> NoteGroups { get; set; }
    public bool Loop { get; set; }
    public bool Gapless { get; set; }
    public MemoryStream DebugStream { get; set; }
}
