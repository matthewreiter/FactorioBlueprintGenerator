using SeeSharp.Runtime.Attributes;

namespace SeeSharp.Runtime
{
    public static class Speaker
    {
        private const int BaseAddress = 33793;
        private const int GoAddress = BaseAddress;
        private const int BaseChannelAddress = BaseAddress + 1;
        private const int ChannelCount = 6;

        [Inline]
        public static void SetChannel(Instrument instrument, int channel, int value)
        {
            Memory.Write((int)instrument + channel, value);
        }

        [Inline]
        public static void SetDrum(int channel, Drum drum)
        {
            Memory.Write((int)Instrument.Drumkit + channel, (int)drum);
        }

        [Inline]
        public static void Go()
        {
            Memory.Write(GoAddress, 1);
        }

        public enum Instrument
        {
            Alarms = BaseChannelAddress,
            Miscellaneous = BaseChannelAddress + ChannelCount,
            Drumkit = BaseChannelAddress + ChannelCount * 2,
            Piano = BaseChannelAddress + ChannelCount * 3,
            Base = BaseChannelAddress + ChannelCount * 4,
            Lead = BaseChannelAddress + ChannelCount * 5,
            Sawtooth = BaseChannelAddress + ChannelCount * 6
        }

        public enum Drum
        {
            Kick1,
            Kick2,
            Snare1,
            Snare2,
            Snare3,
            HiHat1,
            HiHat2,
            Fx,
            HighQ,
            Percussion1,
            Percussion2,
            Crash,
            ReverseCymbal,
            Clap,
            Shaker,
            Cowbell,
            Triangle
        }
    }
}
