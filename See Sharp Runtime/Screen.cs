using SeeSharp.Runtime.CompilerServices;

namespace SeeSharp.Runtime
{
    public static class Screen
    {
        private const int BaseAddress = 37889;
        private const int ClearScreenAddress = BaseAddress;

        [Inline]
        public static void ClearScreen(PixelValue value = PixelValue.Clear)
        {
            Memory.Write(ClearScreenAddress, (int)value);
        }

        public enum PixelValue
        {
            Clear = -1,
            Ignore = 0,
            Set = 1
        }
    }
}
