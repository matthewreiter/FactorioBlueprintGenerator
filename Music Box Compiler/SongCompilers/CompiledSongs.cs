using BlueprintGenerator;
using System.Collections.Generic;

namespace MusicBoxCompiler.SongCompilers;

public class CompiledSongs
{
    public int TotalPlayTime { get; init; }
    public List<MemoryCell> SongCells { get; init; }
    public List<MemoryCell> MetadataCells { get; init; }
    public Dictionary<string, int> PlaylistAddresses { get; init; }
    public Dictionary<string, int> SongMetadataAddresses { get; init; }
}
