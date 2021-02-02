using System;
using System.Collections.Generic;
using System.Drawing;

namespace MemoryInitializer
{
    public static class FontUtil
    {
        public static Font ReadFont(string fontImageFile)
        {
            using var fontImage = new Bitmap(fontImageFile);

            static bool IsRed(Color color) => color.R > 192 && color.G < 64 && color.B < 64;

            var width = 0;
            for (int x = 1; x < fontImage.Width; x++)
            {
                if (IsRed(fontImage.GetPixel(x, 1)))
                {
                    width = x - 1;
                    break;
                }
            }

            if (width == 0)
            {
                throw new Exception("Unable to determine glyph width");
            }

            var height = 0;
            for (int y = 1; y < fontImage.Height; y++)
            {
                if (IsRed(fontImage.GetPixel(1, y)))
                {
                    height = y - 1;
                    break;
                }
            }

            if (height == 0)
            {
                throw new Exception("Unable to determine glyph height");
            }

            var fullWidth = width + 1;
            var fullHeight = height + 2;

            var horizontalGlyphs = (fontImage.Width - 1) / fullWidth;
            var verticalGlyphs = fontImage.Height / fullHeight;

            var characters = new List<Character>();
            var currentCharacterCode = 0;

            for (int row = 0; row < verticalGlyphs; row++)
            {
                for (int column = 0; column < horizontalGlyphs; column++)
                {
                    var glyphPixels = new bool[height, width];
                    var hasPixels = false;

                    var baseCharacterIndicator = fontImage.GetPixel(column * fullWidth, row * fullHeight);
                    if (baseCharacterIndicator.R == 0)
                    {
                        currentCharacterCode = baseCharacterIndicator.G;
                    }
                    else
                    {
                        currentCharacterCode++;
                    }

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            var pixel = fontImage.GetPixel(column * fullWidth + x + 1, row * fullHeight + y + 1);

                            if (pixel.ToArgb() == Color.Black.ToArgb())
                            {
                                glyphPixels[y, x] = true;
                                hasPixels = true;
                            }
                        }
                    }

                    if (hasPixels)
                    {
                        characters.Add(new Character
                        {
                            CharacterCode = currentCharacterCode,
                            GlyphPixels = glyphPixels
                        });
                    }
                }
            }

            return new Font
            {
                Width = width,
                Height = height,
                Characters = characters
            };
        }

        public class Font
        {
            public int Width { get; init; }
            public int Height { get; init; }
            public List<Character> Characters { get; init; }
        }

        public class Character
        {
            public int CharacterCode { get; init; }
            public bool[,] GlyphPixels { get; init; }
        }
    }
}
