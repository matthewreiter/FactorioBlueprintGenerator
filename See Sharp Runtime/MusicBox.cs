using SeeSharp.Runtime.CompilerServices;

namespace SeeSharp.Runtime
{
    public static class MusicBox
    {
        private const int BaseAddress = 34817;
        private const int CurrentPositionAddress = BaseAddress;

        public static int Position
        {
            [Inline]
            get { return Memory.Read(CurrentPositionAddress); }
            [Inline]
            set { Memory.Write(CurrentPositionAddress, value - 4); }
        }
    }
}
