using BlueprintCommon;
using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using BlueprintGenerator.Constants;
using BlueprintGenerator.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace BlueprintGenerator;

public class NoteDisplayByInstrumentGenerator : IBlueprintGenerator
{
    private static readonly Dictionary<Instrument, InstrumentInfo> Instruments = new()
    {
        [Instrument.Drumkit] = new(0, 17),
        [Instrument.Piano] = new(12, 48),
        [Instrument.BassGuitar] = new(0, 36),
        [Instrument.LeadGuitar] = new(0, 36),
        [Instrument.Sawtooth] = new(0, 36),
        [Instrument.Square] = new(0, 36),
        [Instrument.Celesta] = new(36, 36),
        [Instrument.Vibraphone] = new(36, 36),
        [Instrument.PluckedStrings] = new(24, 36),
        [Instrument.SteelDrum] = new(12, 36),
    };

    public Blueprint Generate(IConfigurationRoot configuration)
    {
        return Generate();
    }

    public static Blueprint Generate()
    {
        var instrumentCount = 10;

        var gridWidth = 72;
        var gridHeight = instrumentCount + ((instrumentCount + 7) / 16 + 1) * 2;
        var xOffset = -gridWidth / 2;
        var yOffset = -gridHeight / 2;

        var entities = new List<Entity>();
        var wires = new List<Wire>();

        for (int instrumentIndex = 0; instrumentIndex < instrumentCount; instrumentIndex++)
        {
            var instrument = (Instrument)(instrumentIndex + 3);
            var instrumentInfo = Instruments[instrument];
            var rowY = instrumentIndex + (instrumentIndex / 16 + 1) * 2 + yOffset;

            Entity previousLamp = null;

            for (int noteIndex = 0; noteIndex < instrumentInfo.NoteCount; noteIndex++)
            {
                var signal = SignalID.Create(MusicBoxSignals.NoteDisplaySignals[noteIndex]);

                var lamp = new Entity
                {
                    Name = ItemNames.Lamp,
                    Position = new Position
                    {
                        X = instrumentInfo.NoteOffset + noteIndex + xOffset,
                        Y = rowY
                    },
                    Control_behavior = new ControlBehavior
                    {
                        Circuit_enabled = true,
                        Circuit_condition = new CircuitCondition
                        {
                            First_signal = signal,
                            Constant = 0,
                            Comparator = Comparators.GreaterThan
                        },
                        Use_colors = true,
                        Color_mode = ColorMode.ColorComponents,
                        Blue_signal = signal
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
            Label = "Note Display by Instrument",
            Icons = [Icon.Create(ItemNames.Lamp)],
            Entities = entities,
            Wires = wires.ToArrayList()
        };
    }

    private record InstrumentInfo(int NoteOffset, int NoteCount);
}
