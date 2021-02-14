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

            var height = 0;
            for (int y = 0; y + 1 < fontImage.Height; y++)
            {
                if (IsRed(fontImage.GetPixel(1, y + 1)))
                {
                    height = y;
                    break;
                }
            }

            if (height == 0)
            {
                throw new Exception("Unable to determine glyph height");
            }

            var fullHeight = height + 2;
            var fullWidth = 0;

            var characters = new List<Character>();
            var currentCharacterCode = 0;
            var maxWidth = 0;

            for (var glyphY = 0; glyphY < fontImage.Height - 1; glyphY += fullHeight)
            {
                for (var glyphX = 0; glyphX < fontImage.Width - 1; glyphX += fullWidth)
                {
                    var width = 0;
                    for (var x = 0; glyphX + x + 1 < fontImage.Width; x++)
                    {
                        if (IsRed(fontImage.GetPixel(glyphX + x + 1, glyphY + 1)))
                        {
                            width = x;
                            break;
                        }
                    }

                    if (width == 0)
                    {
                        break;
                    }

                    fullWidth = width + 1;

                    if (width > maxWidth)
                    {
                        maxWidth = width;
                    }

                    var baseCharacterIndicator = fontImage.GetPixel(glyphX, glyphY);
                    if (baseCharacterIndicator.R == 0)
                    {
                        currentCharacterCode = baseCharacterIndicator.G;
                    }
                    else
                    {
                        currentCharacterCode++;
                    }

                    var glyphPixels = new bool[height, width];
                    for (var y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            var pixel = fontImage.GetPixel(glyphX + x + 1, glyphY + y + 1);
                            glyphPixels[y, x] = pixel.ToArgb() == Color.Black.ToArgb();
                        }
                    }

                    characters.Add(new Character
                    {
                        CharacterCode = currentCharacterCode,
                        GlyphPixels = glyphPixels
                    });
                }
            }

            return new Font
            {
                Width = maxWidth,
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
