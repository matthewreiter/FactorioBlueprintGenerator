using Commons.Music.Midi;
using System.Collections.Generic;
using System.Linq;

namespace MusicBoxCompiler.Utils;

// From https://github.com/atsushieno/managed-midi/blob/master/Commons.Music.Midi.Shared/SMF.cs

public class SmfTrackMerger2
{
    public static MidiMusic Merge(MidiMusic source)
    {
        return new SmfTrackMerger2(source).GetMergedMessages();
    }

    private SmfTrackMerger2(MidiMusic source)
    {
        this.source = source;
    }

    private readonly MidiMusic source;

    // FIXME: it should rather be implemented to iterate all
    // tracks with index to messages, pick the track which contains
    // the nearest event and push the events into the merged queue.
    // It's simpler, and costs less by removing sort operation
    // over thousands of events.
    private MidiMusic GetMergedMessages()
    {
        if (source.Format == 0)
        {
            return source;
        }

        var messages = new List<MidiMessage>();

        foreach (var track in source.Tracks)
        {
            int delta = 0;
            foreach (var mev in track.Messages)
            {
                delta += mev.DeltaTime;
                messages.Add(new MidiMessage(delta, mev.Event));
            }
        }

        if (messages.Count == 0)
        {
            return new MidiMusic() { DeltaTimeSpec = source.DeltaTimeSpec }; // empty (why did you need to sort your song file?)
        }

        messages = [.. messages.OrderBy(m => m.DeltaTime).ThenBy(GetMessagePriority)];

        var waitToNext = messages[0].DeltaTime;
        for (int i = 0; i < messages.Count - 1; i++)
        {
            if (messages[i].Event.Value != 0)
            { // if non-dummy
                var tmp = messages[i + 1].DeltaTime - messages[i].DeltaTime;
                messages[i] = new MidiMessage(waitToNext, messages[i].Event);
                waitToNext = tmp;
            }
        }
        messages[^1] = new MidiMessage(waitToNext, messages[^1].Event);

        var m = new MidiMusic
        {
            DeltaTimeSpec = source.DeltaTimeSpec,
            Format = 0
        };
        m.Tracks.Add(new MidiTrack(messages));
        return m;
    }

    private int GetMessagePriority(MidiMessage message)
    {
        return message.Event.EventType switch
        {
            MidiEvent.Program => 0,
            MidiEvent.CC => 1,
            MidiEvent.NoteOn => 2,
            MidiEvent.NoteOff => 3,
            _ => 4,
        };
    }
}
