using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static MemoryInitializer.ConnectionUtil;
using Color = System.Drawing.Color;
using Icon = BlueprintCommon.Models.Icon;

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
            var combinatorsPerRow = configuration.CombinatorsPerRow ?? 5;
            var inputSignal = configuration.InputSignal ?? VirtualSignalNames.Dot;
            var signals = configuration.Signals.Contains(',') ? configuration.Signals.Split(',').ToList() : configuration.Signals.Select(signal => VirtualSignalNames.LetterOrDigit(signal)).ToList();

            const int maxFilters = 20;

            var fullWidth = width + 1;
            var fullHeight = height + 2;

            using var fontImage = new Bitmap(fontImageFile);
            var horizontalGlyphs = (fontImage.Width - 1) / fullWidth;
            var verticalGlyphs = fontImage.Height / fullHeight;

            var entities = new List<Entity>();
            var currentCharacterNumber = 0;
            var currentCharacterCode = 0;
            var previousRowEntityNumber = 0;
            var previousColumnEntityNumber = 0;
            var combinatorX = 0;

            for (int row = 0; row < verticalGlyphs; row++)
            {
                for (int column = 0; column < horizontalGlyphs; column++)
                {
                    var glyphSignals = new List<SignalID>();

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
                                glyphSignals.Add(SignalID.Create(signals[y * width + x]));
                            }
                        }
                    }

                    if (glyphSignals.Count > 0)
                    {
                        var combinatorY = currentCharacterNumber / combinatorsPerRow;
                        var currentCharacterEntityNumber = entities.Count + 1;
                        var adjacentCharacterEntityNumber = currentCharacterNumber % combinatorsPerRow == 0 ? previousRowEntityNumber : previousColumnEntityNumber;

                        entities.Add(new Entity
                        {
                            Entity_number = entities.Count + 1,
                            Name = ItemNames.DeciderCombinator,
                            Position = new Position
                            {
                                X = (currentCharacterNumber % combinatorsPerRow - combinatorsPerRow) * 2 + 0.5,
                                Y = combinatorY
                            },
                            Direction = Direction.Left,
                            Control_behavior = new ControlBehavior
                            {
                                Decider_conditions = new DeciderConditions
                                {
                                    First_signal = SignalID.Create(inputSignal),
                                    Constant = currentCharacterCode,
                                    Comparator = Comparators.IsEqual,
                                    Output_signal = SignalID.Create(VirtualSignalNames.Everything),
                                    Copy_count_from_input = true
                                }
                            },
                            Connections = CreateConnections(new ConnectionPoint
                            {
                                Green = new List<ConnectionData>
                                {
                                    // Connect to constant combinators holding glyphs
                                    new ConnectionData
                                    {
                                        Entity_id = entities.Count + 2
                                    }
                                },
                                Red = currentCharacterNumber > 0 ? new List<ConnectionData>
                                {
                                    // Connect inputs together
                                    new ConnectionData
                                    {
                                        Entity_id = adjacentCharacterEntityNumber,
                                        Circuit_id = CircuitIds.Input
                                    }
                                } : null
                            }, new ConnectionPoint
                            {
                                Green = currentCharacterNumber > 0 ? new List<ConnectionData>
                                {
                                    // Connect outputs together
                                    new ConnectionData
                                    {
                                        Entity_id = adjacentCharacterEntityNumber,
                                        Circuit_id = CircuitIds.Output
                                    }
                                } : null
                            })
                        });

                        for (int index = 0; index < (glyphSignals.Count + maxFilters - 1) / maxFilters; index++)
                        {
                            entities.Add(new Entity
                            {
                                Entity_number = entities.Count + 1,
                                Name = ItemNames.ConstantCombinator,
                                Position = new Position
                                {
                                    X = combinatorX++,
                                    Y = combinatorY
                                },
                                Direction = Direction.Down,
                                Control_behavior = new ControlBehavior
                                {
                                    Filters = glyphSignals.Skip(index * maxFilters).Take(maxFilters).Select(signal => new Filter { Signal = signal, Count = 1 }).ToList()
                                },
                                Connections = CreateConnections(new ConnectionPoint
                                {
                                    Green = new List<ConnectionData>
                                    {
                                        new ConnectionData
                                        {
                                            Entity_id = entities.Count
                                        }
                                    }
                                })
                            });
                        }

                        currentCharacterNumber++;

                        if (currentCharacterNumber % combinatorsPerRow == 0)
                        {
                            combinatorX = 0;
                        }
                        else if (currentCharacterNumber % combinatorsPerRow == 1)
                        {
                            previousRowEntityNumber = currentCharacterEntityNumber;
                        }

                        previousColumnEntityNumber = currentCharacterEntityNumber;
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
                        Signal = SignalID.Create(ItemNames.ConstantCombinator)
                    },
                    new Icon
                    {
                        Signal = SignalID.CreateLetterOrDigit('A')
                    },
                    new Icon
                    {
                        Signal = SignalID.CreateLetterOrDigit('B')
                    },
                    new Icon
                    {
                        Signal = SignalID.CreateLetterOrDigit('C')
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
        public int? CombinatorsPerRow { get; init; }
        public string InputSignal { get; init; }
        public string Signals { get; init; }
    }
}
