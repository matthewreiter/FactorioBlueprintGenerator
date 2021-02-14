using SeeSharp.Runtime.CompilerServices;

namespace SeeSharp.Runtime
{
    public static class Screen
    {
        private const int BaseAddress = 37889;
        private const int ClearScreenAddress = BaseAddress;
        private const int ScreenColorAddress = BaseAddress + 1;
        private const int CharacterXAddress = BaseAddress + 2;
        private const int CharacterYAddress = BaseAddress + 3;
        private const int FontAddress = BaseAddress + 4;
        private const int CharacterAddress = BaseAddress + 5;

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

        public static int CharacterX
        {
            [Inline]
            get => Memory.Read(CharacterXAddress);
            set => Memory.Write(CharacterXAddress, value);
        }

        public static int CharacterY
        {
            [Inline]
            get => Memory.Read(CharacterYAddress);
            set => Memory.Write(CharacterYAddress, value);
        }

        public static int Font
        {
            [Inline]
            get => Memory.Read(FontAddress);
            set => Memory.Write(FontAddress, value);
        }

        [Inline]
        public static void PrintCharacter(char character)
        {
            Memory.Write(CharacterAddress, character);
        }

        public static void Print(char[] text)
        {
            var length = text.Length;
            for (int index = 0; index < length; index++)
            {
                PrintCharacter(text[index]);
            }
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
