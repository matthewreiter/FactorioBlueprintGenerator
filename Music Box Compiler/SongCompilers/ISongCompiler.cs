using MusicBoxCompiler.Models;
using System.Collections.Generic;

namespace MusicBoxCompiler.SongCompilers;

public interface ISongCompiler
{
    int? MaxConcurrentNotes { get; }
    bool SupportsSustainedNotes { get; }
    CompiledSongs CompileSongs(List<Playlist> playlists, MusicBoxConfiguration configuration);
}
