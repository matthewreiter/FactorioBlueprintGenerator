using System.Collections.Generic;

namespace MusicBoxCompiler.Models
{
    public class Playlist
    {
        public List<Song> Songs { get; set; }
        public bool Loop { get; set; }
    }
}
