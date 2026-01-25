using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace BlueprintGenerator;

public class NoteDisplayGenerator : IBlueprintGenerator
{
    public Blueprint Generate(IConfigurationRoot configuration)
    {
        return Generate(configuration.Get<NoteDisplayConfiguration>());
    }

    public static Blueprint Generate(NoteDisplayConfiguration configuration)
    {
        var width = 84;
        var height = configuration.Height ?? 1;

        var gridWidth = width;
        var gridHeight = height + ((height + 7) / 16 + 1) * 2;
        var xOffset = -gridWidth / 2;
        var yOffset = -gridHeight / 2;

        var pitchSignal = SignalID.CreateVirtual(VirtualSignalNames.Explosion);
        var volumeSignal = SignalID.CreateVirtual(VirtualSignalNames.Alarm);

        var entities = new List<Entity>();
        var wires = new List<Wire>();

        for (int row = 0; row < height; row++)
        {
            var rowY = row + (row / 16 + 1) * 2 + yOffset;

            Entity previousLamp = null;

            for (int column = 0; column < width; column++)
            {
                var lamp = new Entity
                {
                    Name = ItemNames.Lamp,
                    Position = new Position
                    {
                        X = column + xOffset,
                        Y = rowY
                    },
                    Control_behavior = new ControlBehavior
                    {
                        Circuit_enabled = true,
                        Circuit_condition = new CircuitCondition
                        {
                            First_signal = pitchSignal,
                            Constant = column + 1,
                            Comparator = Comparators.IsEqual
                        },
                        Use_colors = true,
                        Color_mode = ColorMode.ColorComponents,
                        Blue_signal = volumeSignal
                    }
                };
                entities.Add(lamp);

                if (column > 0)
                {
                    wires.Add(new((lamp, ConnectionType.Green1), (previousLamp, ConnectionType.Green1)));
                }

                previousLamp = lamp;
            }
        }

        BlueprintUtil.PopulateEntityNumbers(entities);

        return new Blueprint
        {
            Label = $"{height}x Note Display",
            Icons = [Icon.Create(ItemNames.Lamp)],
            Entities = entities,
            Wires = wires.ToArrayList()
        };
    }
}

public class NoteDisplayConfiguration
{
    public int? Height { get; set; }
}
