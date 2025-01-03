﻿using BlueprintCommon;
using BlueprintCommon.Models;
using BlueprintGenerator.Screen;
using Codec;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VideoCompiler
{
    public class VideoCompiler
    {
        public static void Run(IConfigurationRoot configuration)
        {
            Run(configuration.Get<VideoConfiguration>());
        }

        public static void Run(VideoConfiguration configuration)
        {
            var videoFile = configuration.VideoFile;
            var outputBlueprintFile = configuration.OutputBlueprint;
            var outputJsonFile = configuration.OutputJson;
            var frameWidth = configuration.FrameWidth ?? 32;
            var frameHeight = configuration.FrameHeight ?? 32;
            var colorMode = configuration.ColorMode ?? ColorMode.Monochrome;
            var ditheringMode = configuration.DitheringMode ?? DitheringMode.None;
            var romHeight = configuration.RomHeight ?? 2;

            var maxFrames = romHeight * 32;

            var videoBuffer = new MemoryStream();

            using (var videoStream = File.OpenRead(videoFile))
            {
                videoStream.CopyTo(videoBuffer);
            }

            var pixelSize = colorMode switch
            {
                ColorMode.Monochrome => 1,
                ColorMode.RedGreenBlue => 2,
                ColorMode.RedGreenBlueWhite => 2,
                _ => throw new Exception($"Unexpected color mode: {colorMode}")
            };

            var palette = colorMode switch
            {
                ColorMode.Monochrome => GeneratePalette(new HdrColor[]
                {
                    HdrColor.FromRgb(1, 1, 1)
                }),
                ColorMode.RedGreenBlue => GeneratePalette(new HdrColor[]
                {
                    HdrColor.FromRgb(1, 0, 0),
                    HdrColor.FromRgb(0, 1, 0),
                    HdrColor.FromRgb(0, 0, 1)
                }),
                ColorMode.RedGreenBlueWhite => GeneratePalette(new HdrColor[]
                {
                    HdrColor.FromRgb(0.8, 0, 0),
                    HdrColor.FromRgb(0, 0.8, 0),
                    HdrColor.FromRgb(0, 0, 0.8),
                    HdrColor.FromRgb(0.8, 0.8, 0.8)
                }),
                _ => throw new Exception($"Unexpected color mode: {colorMode}")
            };

            // Information on dithering: https://cmitja.files.wordpress.com/2015/01/hellandtanner_imagedithering11algorithms.pdf
            var ditheringWeights = ditheringMode switch
            {
                DitheringMode.None => Array.Empty<DitheringWeight>(),
                DitheringMode.Sierra => new DitheringWeight[]
                {
                    new DitheringWeight(5 / 32d, 1, 0),
                    new DitheringWeight(3 / 32d, 2, 0),
                    new DitheringWeight(2 / 32d, -2, 1),
                    new DitheringWeight(4 / 32d, -1, 1),
                    new DitheringWeight(5 / 32d, 0, 1),
                    new DitheringWeight(4 / 32d, 1, 1),
                    new DitheringWeight(2 / 32d, 2, 1),
                    new DitheringWeight(2 / 32d, -1, 2),
                    new DitheringWeight(3 / 32d, 0, 2),
                    new DitheringWeight(2 / 32d, 1, 2),
                },
                DitheringMode.SierraLite => new DitheringWeight[]
                {
                    new DitheringWeight(2 / 4d, 1, 0),
                    new DitheringWeight(1 / 4d, -1, 1),
                    new DitheringWeight(1 / 4d, 0, 1)
                },
                DitheringMode.Temporal => new DitheringWeight[]
                {
                    new DitheringWeight(2 / 6d, 1, 0, 0),
                    new DitheringWeight(1 / 6d, -1, 1, 0),
                    new DitheringWeight(1 / 6d, 0, 1, 0),
                    new DitheringWeight(2 / 6d, 0, 0, 1)
                },
                _ => throw new Exception($"Unexpected dithering mode: {ditheringMode}")
            };

            var rawFrameWidth = frameWidth / pixelSize;
            var rawFrameHeight = frameHeight / pixelSize;

            var video = new Video(videoBuffer, rawFrameWidth, rawFrameHeight);
            var rawFrame = new int[rawFrameWidth * rawFrameHeight];
            var frames = new List<bool[,]>();

            var ditheringFrames = ditheringWeights.Max(ditheringWeight => ditheringWeight.Frame) + 1;
            var colorErrors = new HdrColor[ditheringFrames][,];
            
            for (var index = 0; index < ditheringFrames; index++)
            {
                colorErrors[index] = new HdrColor[frameHeight, frameWidth];
            }

            while (video.AdvanceFrame(rawFrame) && frames.Count < maxFrames)
            {
                var frame = new bool[frameHeight, frameWidth];
                frames.Add(frame);

                var totalBrightness = 0d;

                for (var rawY = 0; rawY < rawFrameHeight; rawY++)
                {
                    for (var rawX = 0; rawX < rawFrameWidth; rawX++)
                    {
                        var color = System.Drawing.Color.FromArgb(rawFrame[rawX + rawY * rawFrameWidth]);
                        totalBrightness += color.GetBrightness();
                    }
                }

                var averageBrightness = totalBrightness / (rawFrameWidth * rawFrameHeight);

                const double gammaMultiplier = 2;
                var gamma = gammaMultiplier * (averageBrightness is > 0.1 and < 0.9 ? Math.Log(0.5, averageBrightness) : 1);

                for (var rawY = 0; rawY < rawFrameHeight; rawY++)
                {
                    for (var rawX = 0; rawX < rawFrameWidth; rawX++)
                    {
                        var x = rawX * pixelSize;
                        var y = rawY * pixelSize;
                        var rawColor = HdrColor.FromArgb(rawFrame[rawX + rawY * rawFrameWidth]);
                        var gammaCorrectedColor = rawColor.Pow(gamma);
                        var ditheredColor = gammaCorrectedColor + colorErrors[0][rawY, rawX];
                        var closestPaletteEntry = GetClosestPaletteEntry(palette, ditheredColor);
                        var newColorError = ditheredColor - closestPaletteEntry.Color;
                        var outputColor = closestPaletteEntry.OutputColor;

                        for (var subPixelY = 0; subPixelY < pixelSize; subPixelY++)
                        {
                            for (var subPixelX = 0; subPixelX < pixelSize; subPixelX++)
                            {
                                var subPixelIndex = subPixelX + subPixelY * pixelSize;

                                if (subPixelIndex < outputColor.Length)
                                {
                                    frame[y + subPixelY, x + subPixelX] = outputColor[subPixelIndex];
                                }
                            }
                        }

                        for (var index = 0; index < ditheringWeights.Length; index++)
                        {
                            var ditheringWeight = ditheringWeights[index];
                            var errorX = rawX + ditheringWeight.X;
                            var errorY = rawY + ditheringWeight.Y;

                            if (errorX >= 0 && errorX < frameWidth && errorY < frameHeight)
                            {
                                colorErrors[ditheringWeight.Frame][errorY, errorX] += newColorError * ditheringWeight.Weight;
                            }
                        }
                    }
                }

                for (var index = 0; index < ditheringFrames - 1; index++)
                {
                    colorErrors[index] = colorErrors[index + 1];
                }

                colorErrors[ditheringFrames - 1] = new HdrColor[frameHeight, frameWidth];

                Console.Write('.');
            }

            Console.WriteLine($"Frames: {frames.Count}");

            var blueprint = VideoRomGenerator.Generate(new VideoMemoryConfiguration
            {
                SnapToGrid = configuration.SnapToGrid,
                X = configuration.X,
                Y = configuration.Y,
                Width = frameWidth,
                Height = romHeight,
                BaseAddress = configuration.BaseAddress
            }, frames);

            BlueprintUtil.PopulateIndices(blueprint);

            var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };

            BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
            BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
        }

        private static PaletteEntry GetClosestPaletteEntry(PaletteEntry[] palette, HdrColor color)
        {
            PaletteEntry closestEntry = null;
            var closestDistance = double.PositiveInfinity;

            for (var palleteIndex = 0; palleteIndex < palette.Length; palleteIndex++)
            {
                var currentEntry = palette[palleteIndex];
                var currentDistance = (currentEntry.Color - color).Length;

                if (currentDistance < closestDistance)
                {
                    closestEntry = currentEntry;
                    closestDistance = currentDistance;
                }
            }

            return closestEntry;
        }

        private static PaletteEntry[] GeneratePalette(HdrColor[] subPixelColors)
        {
            var palette = new PaletteEntry[1 << subPixelColors.Length];

            for (var paletteIndex = 0; paletteIndex < palette.Length; paletteIndex++)
            {
                var currentColor = new HdrColor();
                var outputColor = new bool[subPixelColors.Length];

                for (var subPixelIndex = 0; subPixelIndex < subPixelColors.Length; subPixelIndex++)
                {
                    if (((paletteIndex >> subPixelIndex) & 1) == 1)
                    {
                        currentColor += subPixelColors[subPixelIndex];
                        outputColor[subPixelIndex] = true;
                    }
                }

                palette[paletteIndex] = new PaletteEntry(currentColor, outputColor);
            }

            return palette;
        }

        private record PaletteEntry(HdrColor Color, bool[] OutputColor);
        private record DitheringWeight(double Weight, int X, int Y, int Frame = 0);
    }

    public class VideoConfiguration
    {
        /// <summary>
        /// The video file to compile.
        /// </summary>
        public string VideoFile { get; set; }

        /// <summary>
        /// The file path to store the generated blueprint.
        /// </summary>
        public string OutputBlueprint { get; set; }

        /// <summary>
        /// An optional file path to store the generated blueprint JSON.
        /// </summary>
        public string OutputJson { get; set; }

        /// <summary>
        /// Whether the blueprint should snap to the grid based on the X and Y offsets.
        /// </summary>
        public bool? SnapToGrid { get; set; }

        /// <summary>
        /// The X offset of the blueprint.
        /// </summary>
        public int? X { get; set; }

        /// <summary>
        /// The Y offset of the blueprint.
        /// </summary>
        public int? Y { get; set; }

        /// <summary>
        /// The width of a video frame, in cells.
        /// </summary>
        public int? FrameWidth { get; set; }

        /// <summary>
        /// The height of a video frame, in cells.
        /// </summary>
        public int? FrameHeight { get; set; }

        /// <summary>
        /// What set of colors to use.
        /// </summary>
        public ColorMode? ColorMode { get; set; }

        /// <summary>
        /// Which dithering algorithm to use.
        /// </summary>
        public DitheringMode? DitheringMode { get; set; }

        /// <summary>
        /// If the color mode is monochrome, indicates the height/width of each dither element.
        /// </summary>
        public int? DitherSize { get; set; }

        /// <summary>
        /// The height of the ROM, in cells.
        /// </summary>
        public int? RomHeight { get; set; }

        /// <summary>
        /// The base address for the video frames.
        /// </summary>
        public int? BaseAddress { get; set; }
    }

    public enum ColorMode
    {
        Monochrome,
        RedGreenBlue,
        RedGreenBlueWhite
    }

    public enum DitheringMode
    {
        None,
        Sierra,
        SierraLite,
        Temporal
    }
}
