using SeeSharp.Runtime.CompilerServices;

namespace SeeSharp.Runtime
{
    public static class MusicBox
    {
        private const int BaseAddress = 34817;
        private const int CurrentPositionAddress = BaseAddress;
        private const int ConstantAddress = BaseAddress + 1;
        private const int PlayingSongMetadataAddress = BaseAddress + 2;

        public enum SongMetadataField
        {
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
            get { return Memory.Read(CurrentPositionAddress); }
            [Inline]
            set { Memory.Write(CurrentPositionAddress, value - 4); }
        }

        [Inline]
        public static void SetCurrentSongMetadata(int address)
        {
            Memory.Write(ConstantAddress, address);
        }

        [Inline]
        public static int GetSongMetadata(SongMetadataField field)
        {
            return Memory.ReadSignal(ConstantAddress, (Signal)field);
        }

        [Inline]
        public static int GetSongAddress(int songConstantAddress)
        {
            SetCurrentSongMetadata(songConstantAddress);
            return GetSongMetadata(SongMetadataField.SongAddress);
        }

        [Inline]
        public static int GetPlayingSongMetadata(SongMetadataField field)
        {
            return Memory.ReadSignal(PlayingSongMetadataAddress, (Signal)field);
        }
    }
}
