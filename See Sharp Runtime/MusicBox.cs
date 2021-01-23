using SeeSharp.Runtime.CompilerServices;

namespace SeeSharp.Runtime
{
    public static class MusicBox
    {
        private const int BaseAddress = 34817;
        private const int CurrentPositionAddress = BaseAddress;
        private const int ConstantAddress = BaseAddress + 1;
        private const int PlayingSongMetadataAddress = BaseAddress + 2;
        private const int RequestedSongAddress = BaseAddress + 3;

        public enum SongMetadataField
        {
            MetadataAddress = Signal.Nine,
            SongAddress = Signal.Zero,
            TrackNumber = Signal.One,
            SongLength = Signal.Two,
            SongName1 = Signal.A,
            SongName2 = Signal.B,
            SongName3 = Signal.C,
            SongName4 = Signal.D,
            SongName5 = Signal.E,
            SongName6 = Signal.F
        }

        public static int Position
        {
            [Inline]
            get => Memory.Read(CurrentPositionAddress);
            [Inline]
            set => Memory.Write(CurrentPositionAddress, value - 4);
        }

        public static int RequestedSong
        {
            [Inline]
            get => Memory.Read(RequestedSongAddress);
        }

        [Inline]
        public static void SelectSongMetadata(int address)
        {
            Memory.Write(ConstantAddress, address);
        }

        [Inline]
        public static int GetSelectedSongMetadata(SongMetadataField field)
        {
            return Memory.ReadSignal(ConstantAddress, (Signal)field);
        }

        [Inline]
        public static int GetPlayingSongMetadata(SongMetadataField field)
        {
            return Memory.ReadSignal(PlayingSongMetadataAddress, (Signal)field);
        }
    }
}
