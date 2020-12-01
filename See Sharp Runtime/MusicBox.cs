using SeeSharp.Runtime.CompilerServices;

namespace SeeSharp.Runtime
{
    public static class MusicBox
    {
        private const int BaseAddress = 34817;
        private const int CurrentPositionAddress = BaseAddress;
        private const int ConstantAddress = BaseAddress + 1;

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
        public static int GetSongMetadata(Signal signal)
        {
            return Memory.ReadSignal(ConstantAddress, signal);
        }

        [Inline]
        public static int GetSongAddress(int songConstantAddress)
        {
            SetCurrentSongMetadata(songConstantAddress);
            return GetSongMetadata(Signal.Zero);
        }
    }
}
