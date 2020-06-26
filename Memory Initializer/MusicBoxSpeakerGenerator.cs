using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using static MemoryInitializer.MemoryUtil;

namespace MemoryInitializer
{
    public static class MusicBoxSpeakerGenerator
    {
        public static Blueprint Generate(IConfigurationRoot configuration)
        {
            return Generate(configuration.Get<MusicBoxSpeakerConfiguration>());
        }

        public static Blueprint Generate(MusicBoxSpeakerConfiguration configuration)
        {
            const int numberOfInstruments = 12;

            var speakersPerConfiguration = configuration.SpeakersPerConfiguration ?? 10;
            var volumeLevels = configuration.VolumeLevels ?? 1;
            var minVolume = configuration.MinVolume ?? 0;
            var maxVolume = configuration.MaxVolume ?? 1;
            var baseAddress = configuration.BaseAddress ?? 0;
            var reverseAddressX = configuration.ReverseAddressX ?? false;
            var reverseAddressY = configuration.ReverseAddressY ?? false;
            var includePower = configuration.IncludePower ?? true;

            var speakersPerVolumeLevel = speakersPerConfiguration * numberOfInstruments;
            var speakerCount = speakersPerVolumeLevel * volumeLevels;
            var width = configuration.Width ?? speakerCount / (configuration.Height ?? 16);
            var height = configuration.Height ?? speakerCount / width;

            const int entitiesPerCell = 2;
            const int cellHeight = 3;

            const int playerEntityOffset = 0;
            const int speakerEntityOffset = 1;

            var gridWidth = width + (includePower ? ((width + 7) / 16 + 1) * 2 : 0);
            var gridHeight = height * cellHeight;
            var xOffset = -gridWidth / 2;
            var yOffset = -gridHeight / 2;

            var entities = new List<Entity>();

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    var relativeAddress = (reverseAddressY ? (height - row - 1) : row) * width + (reverseAddressX ? (width - column - 1) : column);
                    var channel = relativeAddress % speakersPerConfiguration;
                    var signal = (char)('0' + channel);
                    var baseEntityNumber = (row * width + column) * entitiesPerCell + 1;
                    var cellX = column + (includePower ? (column / 16 + 1) * 2 : 0) + xOffset;
                    var cellY = (height - row - 1) * cellHeight + 0.5 + yOffset;

                    var adjacentMemoryCells = new List<int> { -1, 1 }
                        .Where(offset => column + offset >= 0 && column + offset < width)
                        .Select(offset => baseEntityNumber + offset * entitiesPerCell)
                        .Concat(new List<int> { -1, 1 }
                            .Where(offset => row + offset >= 0 && row + offset < height && column == 0)
                            .Select(offset => baseEntityNumber + offset * width * entitiesPerCell)
                        )
                        .ToList();

                    // Player
                    entities.Add(new Entity
                    {
                        Entity_number = baseEntityNumber + playerEntityOffset,
                        Name = ItemNames.DeciderCombinator,
                        Position = new Position
                        {
                            X = cellX,
                            Y = cellY
                        },
                        Direction = 4,
                        Control_behavior = new ControlBehavior
                        {
                            Decider_conditions = new DeciderConditions
                            {
                                First_signal = SignalID.CreateLetterOrDigit((char)('A' + channel)),
                                Constant = relativeAddress / speakersPerConfiguration + baseAddress,
                                Comparator = Comparators.IsEqual,
                                Output_signal = SignalID.CreateLetterOrDigit(signal),
                                Copy_count_from_input = true
                            }
                        },
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            // Connection to adjacent player input (address signal and data in)
                            Red = adjacentMemoryCells.Select(entityNumber => new ConnectionData
                            {
                                Entity_id = entityNumber + playerEntityOffset,
                                Circuit_id = CircuitIds.Input
                            }).ToList()
                        }, new ConnectionPoint
                        {
                            Green = new List<ConnectionData>
                            {
                                // Connection to speaker (data out)
                                new ConnectionData
                                {
                                    Entity_id = baseEntityNumber + speakerEntityOffset
                                }
                            }
                        })
                    });

                    // Speaker
                    entities.Add(new Entity
                    {
                        Entity_number = baseEntityNumber + speakerEntityOffset,
                        Name = ItemNames.ProgrammableSpeaker,
                        Position = new Position
                        {
                            X = cellX,
                            Y = cellY + 1.5
                        },
                        Control_behavior = new ControlBehavior
                        {
                            Circuit_condition = new CircuitCondition
                            {
                                First_signal = SignalID.CreateLetterOrDigit(signal)
                            },
                            Circuit_parameters = new CircuitParameters
                            {
                                Signal_value_is_pitch = true,
                                Instrument_id = relativeAddress % speakersPerVolumeLevel / speakersPerConfiguration
                            }
                        },
                        Connections = CreateConnections(new ConnectionPoint
                        {
                            Green = new List<ConnectionData>
                            {
                                // Connection to player output (data in)
                                new ConnectionData
                                {
                                    Entity_id = baseEntityNumber + playerEntityOffset,
                                    Circuit_id = CircuitIds.Output
                                }
                            }
                        }),
                        Parameters = new SpeakerParameter
                        {
                            Playback_volume = maxVolume - (double)(relativeAddress / speakersPerVolumeLevel) / (volumeLevels - 1) * (maxVolume - minVolume),
                            Playback_globally = true,
                            Allow_polyphony = true
                        },
                        Alert_parameters = new SpeakerAlertParameter
                        {
                            Show_alert = false
                        }
                    });
                }
            }

            if (includePower)
            {
                var substationWidth = (width + 7) / 16 + 1;
                var substationHeight = (gridHeight + 3) / 18 + 1;

                entities.AddRange(CreateSubstations(substationWidth, substationHeight, xOffset, yOffset, width * height * entitiesPerCell + 1));
            }

            return new Blueprint
            {
                Label = $"{width}x{height} Music Box Speaker",
                Icons = new List<Icon>
                {
                    new Icon
                    {
                        Signal = new SignalID
                        {
                            Type = SignalTypes.Item,
                            Name = ItemNames.ProgrammableSpeaker
                        }
                    }
                },
                Entities = entities,
                Item = ItemNames.Blueprint,
                Version = BlueprintVersions.CurrentVersion
            };
        }
    }

    public class MusicBoxSpeakerConfiguration
    {
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int? SpeakersPerConfiguration { get; set; }
        public int? VolumeLevels { get; set; }
        public double? MinVolume { get; set; }
        public double? MaxVolume { get; set; }
        public int? BaseAddress { get; set; }
        public bool? ReverseAddressX { get; set; }
        public bool? ReverseAddressY { get; set; }
        public bool? IncludePower { get; set; }
    }
}
