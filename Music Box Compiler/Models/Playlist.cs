using System.Collections.Generic;

namespace MusicBoxCompiler.Models;

public class Playlist
{
    public string Name { get; set; }
    public List<Song> Songs { get; set; }
    public bool Loop { get; set; }
}
