using System.Runtime.CompilerServices;

namespace SeeSharp.Runtime
{
    public static class Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.ForwardRef)]
        public static int Read(int address)
        {
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.ForwardRef)]
        public static int ReadSignal(int address, Signal signal)
        {
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.ForwardRef)]
        public static void Write(int address, int value) { }
    }
}
