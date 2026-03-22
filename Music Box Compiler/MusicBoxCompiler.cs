using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator;
using BlueprintGenerator.Constants;
using Microsoft.Extensions.Configuration;
using MusicBoxCompiler.Models;
using MusicBoxCompiler.SongCompilers;
using MusicBoxCompiler.SongReaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MusicBoxCompiler;

public static class MusicBoxCompiler
{
    public static void Run(IConfigurationRoot configuration)
    {
        Run(configuration.Get<MusicBoxConfiguration>());
    }

    public static void Run(MusicBoxConfiguration configuration)
    {
        var configFile = configuration.ConfigFile;
        var outputBlueprintFile = configuration.OutputBlueprint;
        var outputJsonFile = configuration.OutputJson;
        var outputConstantsFile = configuration.OutputConstants;
        var outputMidiEventsFile = configuration.OutputMidiEvents;
        var version = configuration.Version ?? 1;
        var baseMetadataAddress = configuration.BaseMetadataAddress ?? 1;
        var constantsNamespace = configuration.ConstantsNamespace ?? "Music";

        ISongCompiler songCompiler = version switch
        {
            1 => new SongCompilerV1(),
            2 => new SongCompilerV2(),
            _ => throw new Exception($"Unsupported version: {version}"),
        };

        var config = LoadConfig(configFile);

        var playlists = config.Playlists
            .AsParallel()
            .Where(playlistConfig => !playlistConfig.Disabled)
            .Select(playlistConfig =>
                new Playlist
                {
                    Name = playlistConfig.Name,
                    Songs = playlistConfig.Songs
                        .SelectMany(songConfig => Path.GetExtension(songConfig.Source).ToLower() switch
                        {
                            ".yaml" => LoadConfig(songConfig.Source).Playlists.Find(playlist => playlist.Name == songConfig.SourcePlaylist)?.Songs ?? [],
                            _ => [songConfig]
                        })
                        .SelectMany(songConfig =>
                        {
                            if (songConfig.Source.Contains('*'))
                            {
                                var directoryName = Path.GetDirectoryName(songConfig.Source);
                                var fileName = Path.GetFileName(songConfig.Source);
                                var files = Directory.GetFiles(directoryName, fileName);

                                if (files.Length == 1)
                                {
                                    return [songConfig with { Source = files[0] }];
                                }
                                else if (files.Length > 1)
                                {
                                    return files.OrderBy(file => file)
                                        .Select((source, index) => songConfig with
                                        {
                                            Name = $"{songConfig.Name}Part{index + 1}",
                                            DisplayName = songConfig.DisplayName != null ? $"{songConfig.DisplayName} (Part {index + 1})" : null,
                                            Source = source,
                                            Gapless = songConfig.Gapless
                                        });
                                }
                            }

                            return [songConfig];
                        })
                        .ToList() // Store the intermediate results as a list to preserve the order when parallelized
                        .AsParallel()
                        .Where(songConfig => !songConfig.Disabled)
                        .Select(songConfig =>
                            Path.GetExtension(songConfig.Source).ToLower() switch
                            {
                                ".xlsx" => SpreadsheetReader.ReadSongFromSpreadsheet(songConfig.Source, songConfig.SpreadsheetTab),
                                ".mid" => MidiReader.ReadSong(
                                    songConfig.Source,
                                    outputMidiEventsFile != null,
                                    songConfig.InstrumentOffsets,
                                    ProcessMasterVolume(songConfig.Volume),
                                    ProcessInstrumentVolumes(songConfig.InstrumentVolumes),
                                    !songConfig.SuppressInstrumentFallback,
                                    songConfig.ExpandNotes && !songCompiler.SupportsSustainedNotes,
                                    songCompiler.MaxConcurrentNotes,
                                    songConfig.Fade),
                                _ => throw new Exception($"Unsupported source file extension for {songConfig.Source}")
                            }
                            with
                            {
                                Name = songConfig.Name,
                                DisplayName = songConfig.DisplayName,
                                Album = songConfig.Album,
                                Artist = songConfig.Artist,
                                Loop = songConfig.Loop,
                                Gapless = songConfig.Gapless,
                                AddressIndex = songConfig.AddressIndex
                            }
                        )
                        .ToList(),
                    Loop = playlistConfig.Loop
                }
            )
            .ToList();

        if (config.IncludeBlankSong)
        {
            playlists.Add(new()
            {
                Songs =
                [
                    new()
                    {
                        Name = "Blank",
                        DisplayName = "",
                        AddressIndex = 1000 - baseMetadataAddress,
                        NoteGroups = [new()
                        {
                            Length = TimeSpan.FromSeconds(10),
                            Notes = [new() { Instrument = Instrument.LeadGuitar, Number = 48 }] // Use a note that is out of range of the instrument
                        }]
                    }
                ]
            });
        }

        var compiledSongs = songCompiler.CompileSongs(playlists, configuration);

        var blueprint = CreateBlueprintFromCompiledSongs(compiledSongs, configuration);
        BlueprintUtil.PopulateIndices(blueprint);

        var blueprintWrapper = new BlueprintWrapper { Blueprint = blueprint };

        BlueprintUtil.WriteOutBlueprint(outputBlueprintFile, blueprintWrapper);
        BlueprintUtil.WriteOutJson(outputJsonFile, blueprintWrapper);
        WriteOutConstants(outputConstantsFile, compiledSongs, constantsNamespace);
        WriteOutMidiEvents(outputMidiEventsFile, playlists);
    }

    private static MusicConfig LoadConfig(string configFile)
    {
        var basePath = Path.GetDirectoryName(configFile);
        using var reader = new StreamReader(configFile);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var config = deserializer.Deserialize<MusicConfig>(reader);

        return config with
        {
            Playlists = [.. config.Playlists.Select(playlistConfig => playlistConfig with
            {
                Songs = [.. playlistConfig.Songs.Select(songConfig => songConfig with
                {
                    Source = Path.Combine(basePath, songConfig.Source)
                })]
            })]
        };
    }

    private static double ProcessMasterVolume(double? masterVolume) => (masterVolume ?? 100) / 100;

    private static Dictionary<Instrument, double> ProcessInstrumentVolumes(Dictionary<Instrument, double> instrumentVolumes) =>
        instrumentVolumes?.Select(entry => (entry.Key, Value: entry.Value / 100))?.ToDictionary(entry => entry.Key, entry => entry.Value);

    private static Blueprint CreateBlueprintFromCompiledSongs(CompiledSongs compiledSongs, MusicBoxConfiguration configuration)
    {
        var snapToGrid = configuration.SnapToGrid;
        var x = configuration.X;
        var y = configuration.Y;
        var width = configuration.Width ?? 16;
        var height = configuration.Height ?? 16;

        var romUsed = compiledSongs.SongCells.Count;
        var totalRom = width * (height - 1);

        Console.WriteLine($"Total play time: {TimeSpan.FromSeconds(compiledSongs.TotalPlayTime / 60d):h\\:mm\\:ss\\.fff}");
        Console.WriteLine($"ROM usage: {romUsed}/{totalRom} ({(double)romUsed / totalRom * 100:F1}%)");

        return RomGenerator.Generate(new RomConfiguration
        {
            SnapToGrid = snapToGrid,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            ProgramRows = 1, // Allocate one line for the constant cells
            ProgramName = "Songs",
            IconNames = [ItemNames.ElectronicCircuit, ItemNames.ProgrammableSpeaker]
        }, compiledSongs.MetadataCells, compiledSongs.SongCells);
    }

    private static void WriteOutConstants(string outputConstantsFile, CompiledSongs compiledSongs, string constantsNamespace)
    {
        if (outputConstantsFile == null)
        {
            return;
        }

        File.WriteAllText(outputConstantsFile, $@"namespace {constantsNamespace}
{{
    public static class PlaylistAddresses
    {{
        {string.Join($"{Environment.NewLine}        ", compiledSongs.PlaylistAddresses.Select(entry => $"public const int {entry.Key} = {entry.Value};"))}
    }}

    public static class SongMetadataAddresses
    {{
        {string.Join($"{Environment.NewLine}        ", compiledSongs.SongMetadataAddresses.Select(entry => $"public const int {entry.Key} = {entry.Value};"))}
    }}
}}
");
    }

    private static void WriteOutMidiEvents(string outputMidiEventsFile, List<Playlist> playlists)
    {
        if (outputMidiEventsFile is not null)
        {
            using var midiEventStream = File.Create(outputMidiEventsFile);

            foreach (var playlist in playlists)
            {
                foreach (var song in playlist.Songs)
                {
                    var debugStream = song.DebugStream;

                    if (debugStream is not null)
                    {
                        debugStream.Position = 0;
                        debugStream.CopyTo(midiEventStream);
                    }
                }
            }
        }
    }
}

public class MusicBoxConfiguration
{
    public string ConfigFile { get; set; }
    public string OutputBlueprint { get; set; }
    public string OutputJson { get; set; }
    public string OutputConstants { get; set; }
    public string OutputMidiEvents { get; set; }
    public int? Version { get; set; }
    public int? BaseAddress { get; set; }
    public int? BaseNoteAddress { get; set; }
    public int? BaseMetadataAddress { get; set; }
    public int? NextAddress { get; set; }
    public bool? SnapToGrid { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? VolumeLevels { get; set; }
    public double? MinVolume { get; set; }
    public double? MaxVolume { get; set; }
    public string ConstantsNamespace { get; set; }
}
