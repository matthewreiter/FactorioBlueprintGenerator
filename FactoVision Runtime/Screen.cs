using FactoVision.Runtime.CompilerServices;

namespace FactoVision.Runtime
{
    public static class Screen
    {
        private const int BaseAddress = 37889;

        private const int ClearScreenAddress = BaseAddress;
        private const int ScreenColorAddress = BaseAddress + 1;

        private const int AutoAdvanceCharacterAddress = BaseAddress + 16;
        private const int CharacterXAddress = BaseAddress + 17;
        private const int CharacterYAddress = BaseAddress + 18;
        private const int FontAddress = BaseAddress + 19;
        private const int CharacterAddress = BaseAddress + 20;

        private const int SpriteXAddress = BaseAddress + 32;
        private const int SpriteYAddress = BaseAddress + 33;
        private const int CurrentSpriteAddress = BaseAddress + 34;
        private const int DrawSpriteAddress = BaseAddress + 35;
        private const int ReadSpriteAddress = BaseAddress + 36;
        private const int WriteSpriteBaseAddress = BaseAddress + 37;

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

        public static bool AutoAdvanceCharacter
        {
            [Inline]
            get => Memory.Read(AutoAdvanceCharacterAddress) != 0;
            [Inline]
            set => Memory.Write(AutoAdvanceCharacterAddress, value ? 1 : 0);
        }

        public static int CharacterX
        {
            [Inline]
            get => Memory.Read(CharacterXAddress);
            [Inline]
            set => Memory.Write(CharacterXAddress, value);
        }

        public static int CharacterY
        {
            [Inline]
            get => Memory.Read(CharacterYAddress);
            [Inline]
            set => Memory.Write(CharacterYAddress, value);
        }

        public static int Font
        {
            [Inline]
            get => Memory.Read(FontAddress);
            [Inline]
            set => Memory.Write(FontAddress, value);
        }

        [Inline]
        public static void Print(char character)
        {
            Memory.Write(CharacterAddress, character);
        }

        public static void Print(char[] text)
        {
            var length = text.Length;
            for (int index = 0; index < length; index++)
            {
                Print(text[index]);
            }
        }

        public static void Print(int value)
        {
            void PrintNumber(int number)
            {
                if (number >= 10)
                {
                    PrintNumber(number / 10);
                }

                Print((char)('0' + number % 10));
            }

            if (value < 0)
            {
                Print('-');
                PrintNumber(-value);
            }
            else
            {
                PrintNumber(value);
            }
        }

        public static int SpriteX
        {
            [Inline]
            get => Memory.Read(SpriteXAddress);
            [Inline]
            set => Memory.Write(SpriteXAddress, value);
        }

        public static int SpriteY
        {
            [Inline]
            get => Memory.Read(SpriteYAddress);
            [Inline]
            set => Memory.Write(SpriteYAddress, value);
        }

        public static int CurrentSprite
        {
            [Inline]
            get => Memory.Read(CurrentSpriteAddress);
            [Inline]
            set => Memory.Write(CurrentSpriteAddress, value);
        }

        [Inline]
        public static void DrawSprite(int sprite)
        {
            Memory.Write(DrawSpriteAddress, sprite);
        }

        [Inline]
        public static void DrawSprite(int sprite, int x, int y)
        {
            Memory.Write(SpriteXAddress, x);
            Memory.Write(SpriteYAddress, y);
            Memory.Write(DrawSpriteAddress, sprite);
        }

        [Inline]
        public static int GetSpriteData(Signal signal)
        {
            return Memory.ReadSignal(ReadSpriteAddress, signal);
        }

        [Inline]
        public static void SetSpriteData(Signal signal, int value)
        {
            Memory.Write(WriteSpriteBaseAddress + (int)signal, value);
        }

        [Inline]
        public static void ClearSprite()
        {
            Memory.Write(WriteSpriteBaseAddress, 1);
        }

        public static void WriteSprite(int sprite, int width, int height, int[] data)
        {
            CurrentSprite = sprite;
            ClearSprite();
            SetSpriteData(Signal.X, width);
            SetSpriteData(Signal.Y, height);

            var length = data.Length;
            for (var index = 0; index < length; index++)
            {
                SetSpriteData(Signal.Zero + index, data[index]);
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
