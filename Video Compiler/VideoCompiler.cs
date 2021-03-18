using BlueprintCommon;
using BlueprintCommon.Models;
using BlueprintGenerator.Screen;
using Codec;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using SysColor = System.Drawing.Color;

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
            var ditherSize = configuration.DitherSize ?? 1;
            var useEdgeDetection = configuration.UseEdgeDetection ?? false;
            var romHeight = configuration.RomHeight ?? 2;

            var maxFrames = romHeight * 32;

            var videoBuffer = new MemoryStream();

            using (var videoStream = File.OpenRead(videoFile))
            {
                videoStream.CopyTo(videoBuffer);
            }

            var basePixelSize = colorMode switch
            {
                ColorMode.Monochrome => 1,
                ColorMode.RedGreenBlue => 2,
                ColorMode.RedGreenBlueWhite => 2,
                _ => throw new Exception($"Unexpected color mode: {colorMode}")
            };
            var pixelSize = basePixelSize * ditherSize;

            var rawFrameWidth = frameWidth / pixelSize;
            var rawFrameHeight = frameHeight / pixelSize;

            var video = new Video(videoBuffer, rawFrameWidth, rawFrameHeight);
            var rawFrame = new int[rawFrameWidth * rawFrameHeight];
            var frames = new List<bool[,]>();

            while (video.AdvanceFrame(rawFrame) && frames.Count < maxFrames)
            {
                var frame = new bool[frameHeight, frameWidth];
                frames.Add(frame);

                var brightnessCutoff = 0d;

                if (ditherSize == 1)
                {
                    var totalBrightness = 0d;

                    for (var rawY = 0; rawY < rawFrameHeight; rawY++)
                    {
                        for (var rawX = 0; rawX < rawFrameWidth; rawX++)
                        {
                            var color = System.Drawing.Color.FromArgb(rawFrame[rawX + rawY * rawFrameWidth]);
                            totalBrightness += color.GetBrightness();
                        }
                    }

                    brightnessCutoff = totalBrightness / (rawFrameWidth * rawFrameHeight);
                }

                for (var rawY = 0; rawY < rawFrameHeight; rawY++)
                {
                    for (var rawX = 0; rawX < rawFrameWidth; rawX++)
                    {
                        var x = rawX * pixelSize;
                        var y = rawY * pixelSize;
                        var color = SysColor.FromArgb(rawFrame[rawX + rawY * rawFrameWidth]);
                        var levels = ditherSize * ditherSize + 1;

                        switch (colorMode)
                        {
                            case ColorMode.Monochrome:
                                {
                                    var brightness = color.GetBrightness();

                                    for (var ditherY = 0; ditherY < ditherSize; ditherY++)
                                    {
                                        for (var ditherX = 0; ditherX < ditherSize; ditherX++)
                                        {
                                            var currentBrightnessCutoff = ditherSize == 1 ? brightnessCutoff : (double)(ditherX + ditherY * ditherSize + 1) / levels;
                                            frame[y + ditherY, x + ditherX] = brightness > currentBrightnessCutoff;
                                        }
                                    }
                                }

                                break;
                            case ColorMode.RedGreenBlue:
                                for (var ditherY = 0; ditherY < ditherSize; ditherY++)
                                {
                                    for (var ditherX = 0; ditherX < ditherSize; ditherX++)
                                    {
                                        var currentBrightnessCutoff = ditherSize == 1 ? brightnessCutoff : (double)(ditherX + ditherY * ditherSize + 1) / levels;
                                        var cutoff = (byte)(currentBrightnessCutoff * 255);
                                        var red = color.R >= cutoff;
                                        var green = color.G >= cutoff;
                                        var blue = color.B >= cutoff;
                                        var baseX = x + ditherX * 2;
                                        var baseY = y + ditherY * 2;

                                        frame[baseY, baseX] = red;
                                        frame[baseY, baseX + 1] = green;
                                        frame[baseY + 1, baseX] = blue;
                                    }
                                }

                                break;
                            case ColorMode.RedGreenBlueWhite:
                                for (var ditherY = 0; ditherY < ditherSize; ditherY++)
                                {
                                    for (var ditherX = 0; ditherX < ditherSize; ditherX++)
                                    {
                                        var currentBrightnessCutoff = ditherSize == 1 ? brightnessCutoff : (double)(ditherX + ditherY * ditherSize + 1) / levels;
                                        var cutoff = (byte)(currentBrightnessCutoff * 255);
                                        var red = color.R >= cutoff;
                                        var green = color.G >= cutoff;
                                        var blue = color.B >= cutoff;

                                        bool white = useEdgeDetection
                                            ? DetectEdge(color, rawFrame, rawX, rawY, rawFrameWidth, -1, 0) ||
                                                DetectEdge(color, rawFrame, rawX, rawY, rawFrameWidth, 0, -1) ||
                                                DetectEdge(color, rawFrame, rawX, rawY, rawFrameWidth, -1, -1)
                                            : red && green && blue && color.GetBrightness() >= (currentBrightnessCutoff + 1) / 2;

                                        var baseX = x + ditherX * 2;
                                        var baseY = y + ditherY * 2;

                                        frame[baseY, baseX] = red;
                                        frame[baseY, baseX + 1] = green;
                                        frame[baseY + 1, baseX] = blue;
                                        frame[baseY + 1, baseX + 1] = white;
                                    }
                                }

                                break;
                        }
                    }
                }
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

        private static bool DetectEdge(SysColor color, int[] rawFrame, int rawX, int rawY, int rawFrameWidth, int deltaX, int deltaY)
        {
            const int edgeCutoff1 = 35;
            const int edgeCutoff2 = 55;

            if (rawX + deltaX * 2 < 0 || rawY + deltaY * 2 < 0)
            {
                return false;
            }

            var adjacentColor1 = SysColor.FromArgb(rawFrame[rawX + deltaX + (rawY + deltaY) * rawFrameWidth]);
            var adjacentColor2 = SysColor.FromArgb(rawFrame[rawX + deltaX * 2 + (rawY + deltaY * 2) * rawFrameWidth]);

            return GetColorDistance(color, adjacentColor1) > edgeCutoff1 &&
                 GetColorDistance(color, adjacentColor2) > edgeCutoff2;
        }

        private static double GetColorDistance(SysColor color1, SysColor color2)
        {
            return Math.Sqrt(Math.Pow(color1.R - color2.R, 2) + Math.Pow(color1.G - color2.G, 2) + Math.Pow(color1.B - color2.B, 2));
        }
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
        /// If the color mode is monochrome, indicates the height/width of each dither element.
        /// </summary>
        public int? DitherSize { get; set; }

        /// <summary>
        /// Whether the white color channel should be set based on edge detection.
        /// </summary>
        public bool? UseEdgeDetection { get; set; }

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
}
