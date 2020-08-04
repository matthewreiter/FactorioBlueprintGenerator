using System.Collections.Generic;

namespace MusicBoxCompiler.Models
{
    public class MusicConfig
    {
        public List<PlaylistConfig> Playlists { get; set; }
    }

    public class PlaylistConfig
    {
        public string Name { get; set; }
        public List<SongConfig> Songs { get; set; }
        public bool Loop { get; set; }
    }

    public class SongConfig
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public string SpreadsheetTab { get; set; }
        public Dictionary<Instrument, int> InstrumentOffsets { get; set; }
        public Dictionary<Instrument, double> InstrumentVolumes { get; set; }
        public double? Volume { get; set; }
        public bool Loop { get; set; }
        public bool Disabled { get; set; }
    }
}
