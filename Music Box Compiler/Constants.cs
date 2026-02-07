using System.Collections.Generic;

namespace MusicBoxCompiler;

public static class Constants
{
    public static readonly List<string> Drums =
    [
        "Kick 1",
        "Kick 2",
        "Snare 1",
        "Snare 2",
        "Snare 3",
        "High Hat 1",
        "High Hat 2",
        "Fx",
        "High Q",
        "Percussion 1",
        "Percussion 2",
        "Crash",
        "Reverse Cymbal",
        "Clap",
        "Shaker",
        "Cowbell",
        "Triangle"
    ];

    /// <summary>
    /// Percussion keys added in General MIDI 2.
    /// </summary>
    public static class PercussionsGM2
    {
        // https://web.archive.org/web/20210515163601/https://www.musicrepo.com/wp-content/uploads/GM2-Drum-Map.txt (subtract 1 since documentation is 1-based but the actual wire protocol is 0-based)
        public const byte HighQ = 26;
        public const byte Slap = 27;
        public const byte ScratchPush = 28;
        public const byte ScratchPull = 29;
        public const byte Sticks = 30;
        public const byte SquareClick = 31;
        public const byte MetronomeClick = 32;
        public const byte MetronomeBell = 33;
        public const byte Shaker = 81;
        public const byte JingleBell = 82;
        public const byte Belltree = 83;
        public const byte Castanets = 84;
        public const byte MuteSurdo = 85;
        public const byte OpenSurdo = 86;
    }
}
