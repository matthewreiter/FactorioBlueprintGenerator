using System;
using System.Runtime.CompilerServices;

namespace SeeSharp.Runtime
{
    public static class Speaker
    {
        private const int BaseAddress = 33793;
        private const int GoAddress = BaseAddress;
        private const int BaseChannelAddress = BaseAddress + 1;
        private const int ChannelCount = 6;
        private const int InstrumentCount = 12;
        private const int SpeakersPerVolumeLevel = ChannelCount * InstrumentCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetChannel(Instrument instrument, int channel, int value)
        {
            Memory.Write((int)instrument + channel, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetChannel(Instrument instrument, int channel, int value, Volume volume)
        {
            Memory.Write((int)instrument + channel + (int)volume, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetDrum(int channel, Drum drum)
        {
            Memory.Write((int)Instrument.Drumkit + channel, (int)drum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetDrum(int channel, Drum drum, Volume volume)
        {
            Memory.Write((int)Instrument.Drumkit + channel + (int)volume, (int)drum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clear()
        {
            Memory.Write(GoAddress, (int)ExecutionFlags.Clear);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Play()
        {
            Memory.Write(GoAddress, (int)ExecutionFlags.Play);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PlayAndClear()
        {
            Memory.Write(GoAddress, (int)(ExecutionFlags.Clear | ExecutionFlags.Play));
        }

        public enum Volume
        {
            Max = 0,
            High = SpeakersPerVolumeLevel,
            Medium = SpeakersPerVolumeLevel * 2,
            Low = SpeakersPerVolumeLevel * 3,
        }

        public enum Instrument
        {
            Alarms = BaseChannelAddress,
            Miscellaneous = BaseChannelAddress + ChannelCount,
            Drumkit = BaseChannelAddress + ChannelCount * 2,
            Piano = BaseChannelAddress + ChannelCount * 3,
            Base = BaseChannelAddress + ChannelCount * 4,
            Lead = BaseChannelAddress + ChannelCount * 5,
            Sawtooth = BaseChannelAddress + ChannelCount * 6,
            Square = BaseChannelAddress + ChannelCount * 7,
            Celesta = BaseChannelAddress + ChannelCount * 8,
            Vibraphone = BaseChannelAddress + ChannelCount * 9,
            PluckedStrings = BaseChannelAddress + ChannelCount * 10,
            SteelDrum = BaseChannelAddress + ChannelCount * 11
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

        [Flags]
        private enum ExecutionFlags
        {
            Clear = 1,
            Play = 2
        }
    }
}
