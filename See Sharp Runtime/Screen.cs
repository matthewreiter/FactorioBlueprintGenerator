using SeeSharp.Runtime.CompilerServices;

namespace SeeSharp.Runtime
{
    public static class Screen
    {
        private const int BaseAddress = 37889;
        private const int ClearScreenAddress = BaseAddress;
        private const int ScreenColorAddress = BaseAddress + 1;

        [Inline]
        public static void ClearScreen(PixelValue value = PixelValue.Clear)
        {
            Memory.Write(ClearScreenAddress, (int)value);
        }

        [Inline]
        public static void SetScreenColor(Color color)
        {
            Memory.Write(ScreenColorAddress, (int)color);
        }

        public enum PixelValue
        {
            Clear = -1,
            Ignore = 0,
            Set = 1
        }

        public enum Color
        {
            None,
            Red,
            Blue,
            Yellow,
            Pink,
            Cyan,
            White
        }
    }
}
