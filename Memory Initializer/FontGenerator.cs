using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Icon = BlueprintCommon.Models.Icon;
using Color = System.Drawing.Color;

namespace MemoryInitializer
{
    public class FontGenerator
    {
        public static Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<FontConfiguration>());
        }

        public static Blueprint Generate(FontConfiguration configuration)
        {
            var fontImageFile = configuration.FontImage;
            var width = configuration.Width;
            var height = configuration.Height;
            var horizontalKerning = configuration.HorizontalKerning ?? 1;
            var verticalKerning = configuration.VerticalKerning ?? 2;
            var signals = configuration.Signals.Contains(',') ? configuration.Signals.Split(',').ToList() : configuration.Signals.Select(signal => VirtualSignalNames.LetterOrDigit(signal)).ToList();

            var fullWidth = width + horizontalKerning;
            var fullHeight = height + verticalKerning;

            using var fontImage = new Bitmap(fontImageFile);
            var horizontalGlyphs = fontImage.Width / fullWidth;
            var verticalGlyphs = fontImage.Height / fullHeight;

            var entities = new List<Entity>();
            var currentCharacterNumber = 1;

            for (int row = 0; row < verticalGlyphs; row++)
            {
                for (int column = 0; column < horizontalGlyphs; column++)
                {
                    var glyphSignals = new List<SignalID>();

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            var pixel = fontImage.GetPixel(column * fullWidth + x, row * fullHeight + y);

                            if (pixel.ToArgb() == Color.Black.ToArgb())
                            {
                                glyphSignals.Add(new SignalID
                                {
                                    Name = signals[y * width + x],
                                    Type = SignalTypes.Virtual
                                });
                            }
                        }
                    }

                    if (glyphSignals.Count > 0)
                    {
                        for (int index = 0; index < (glyphSignals.Count + 17) / 18; index++)
                        {
                            entities.Add(new Entity
                            {
                                Entity_number = entities.Count + 1,
                                Name = ItemNames.ConstantCombinator,
                                Position = new Position
                                {
                                    X = currentCharacterNumber,
                                    Y = index
                                },
                                Direction = 4,
                                Control_behavior = new ControlBehavior
                                {
                                    Filters = glyphSignals.Skip(index * 18).Take(18).Select(signal => new Filter { Signal = signal, Count = 1 }).ToList()
                                }
                            });
                        }

                        currentCharacterNumber++;
                    }
                }
            }

            return new Blueprint
            {
                Label = $"{width}x{height} Font",
                Icons = new List<Icon>
                {
                    new Icon
                    {
                        Signal = new SignalID
                        {
                            Type = SignalTypes.Item,
                            Name = ItemNames.ConstantCombinator
                        }
                    },
                    new Icon
                    {
                        Signal = new SignalID
                        {
                            Type = SignalTypes.Virtual,
                            Name = VirtualSignalNames.LetterOrDigit('A')
                        }
                    },
                    new Icon
                    {
                        Signal = new SignalID
                        {
                            Type = SignalTypes.Virtual,
                            Name = VirtualSignalNames.LetterOrDigit('B')
                        }
                    },
                    new Icon
                    {
                        Signal = new SignalID
                        {
                            Type = SignalTypes.Virtual,
                            Name = VirtualSignalNames.LetterOrDigit('C')
                        }
                    }
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };
        }
    }

    public class FontConfiguration
    {
        public string FontImage { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public int? HorizontalKerning { get; init; }
        public int? VerticalKerning { get; init; }
        public string Signals { get; init; }
    }
}
