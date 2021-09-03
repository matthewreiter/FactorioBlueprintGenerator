using System.Collections.Generic;

namespace MusicBoxCompiler.Models
{
    public record MusicConfig
    {
        public bool IncludeBlankSong { get; set; }
        public List<PlaylistConfig> Playlists { get; set; }
    }

    public record PlaylistConfig
    {
        public string Name { get; set; }
        public bool Loop { get; set; }
        public bool Disabled { get; set; }
        public List<SongConfig> Songs { get; set; }
    }

    public record SongConfig
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Artist { get; set; }
        /// <summary>
        /// Indicates where the constant field holding the song's address should be located in memory relative to the base constant address.
        /// </summary>
        public int? AddressIndex { get; set; }
        /// <summary>
        /// May be an absolute path or a path relative to the music config.
        /// </summary>
        public string Source { get; set; }
        public string SourcePlaylist { get; set; }
        public string SpreadsheetTab { get; set; }
        public Dictionary<Instrument, int> InstrumentOffsets { get; set; }
        public Dictionary<Instrument, double> InstrumentVolumes { get; set; }
        public double? Volume { get; set; }
        public bool SuppressInstrumentFallback { get; set; }
        public bool Loop { get; set; }
        /// <summary>
        /// Whether to suppress the gap between this song and the next.
        /// </summary>
        public bool Gapless { get; set; }
        public bool Disabled { get; set; }
    }
}
