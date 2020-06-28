using System.Runtime.CompilerServices;

namespace SeeSharp.Runtime
{
    public static class MusicBox
    {
        private const int BaseAddress = 34817;
        private const int CurrentPositionAddress = BaseAddress;

        public static int Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Memory.Read(CurrentPositionAddress); }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { Memory.Write(CurrentPositionAddress, value); }
        }
    }
}
