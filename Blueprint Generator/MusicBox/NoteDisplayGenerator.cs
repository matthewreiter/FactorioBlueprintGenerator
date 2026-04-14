using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace BlueprintGenerator.MusicBox;

public class NoteDisplayGenerator : IBlueprintGenerator
{
    public Blueprint Generate(IConfigurationRoot configuration)
    {
        return Generate(configuration.Get<NoteDisplayConfiguration>());
    }

    public static Blueprint Generate(NoteDisplayConfiguration configuration)
    {
        var noteCount = 72;
        var voiceCount = configuration.VoiceCount ?? 1;

        var gridWidth = noteCount;
        var gridHeight = voiceCount + ((voiceCount + 7) / 16 + 1) * 2;
        var xOffset = -gridWidth / 2;
        var yOffset = -gridHeight / 2;

        var pitchSignal = SignalID.CreateVirtual(VirtualSignalNames.Explosion);
        var volumeSignal = SignalID.CreateVirtual(VirtualSignalNames.Alarm);

        var entities = new List<Entity>();
        var wires = new List<Wire>();

        for (int voiceIndex = 0; voiceIndex < voiceCount; voiceIndex++)
        {
            var rowY = voiceIndex + (voiceIndex / 16 + 1) * 2 + yOffset;

            Entity previousLamp = null;

            for (int noteIndex = 0; noteIndex < noteCount; noteIndex++)
            {
                var lamp = new Entity
                {
                    Name = ItemNames.Lamp,
                    Position = new Position
                    {
                        X = noteIndex + xOffset,
                        Y = rowY
                    },
                    Control_behavior = new ControlBehavior
                    {
                        Circuit_enabled = true,
                        Circuit_condition = new CircuitCondition
                        {
                            First_signal = pitchSignal,
                            Constant = noteIndex + 1,
                            Comparator = Comparators.IsEqual
                        },
                        Use_colors = true,
                        Color_mode = ColorMode.ColorComponents,
                        Blue_signal = volumeSignal
                    }
                };
                entities.Add(lamp);

                if (noteIndex > 0)
                {
                    wires.Add(new((lamp, ConnectionType.Green1), (previousLamp, ConnectionType.Green1)));
                }

                previousLamp = lamp;
            }
        }

        BlueprintUtil.PopulateEntityNumbers(entities);

        return new Blueprint
        {
            Label = $"{voiceCount}x Note Display",
            Icons = [Icon.Create(ItemNames.Lamp)],
            Entities = entities,
            Wires = wires.ToArrayList()
        };
    }
}

public class NoteDisplayConfiguration
{
    public int? VoiceCount { get; set; }
}
