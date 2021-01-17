using SeeSharp.Runtime.CompilerServices;

namespace SeeSharp.Runtime
{
    public static class Clock
    {
        private const int BaseAddress = 36865;
        public const int AbsoluteTicksAddress = BaseAddress;
        public const int RelativeTicksAddress = BaseAddress + 1;

        public static int AbsoluteTicks
        {
            [Inline]
            get { return Memory.Read(AbsoluteTicksAddress); }
        }

        public static int RelativeTicks
        {
            [Inline]
            get { return Memory.Read(RelativeTicksAddress); }
        }
    }
}
