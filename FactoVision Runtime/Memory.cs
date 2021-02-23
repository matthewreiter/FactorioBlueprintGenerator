using FactoVision.Runtime.CompilerServices;
using System.Runtime.CompilerServices;

namespace FactoVision.Runtime
{
    public static class Memory
    {
        [Inline]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static int Read(int address)
        {
            return 0;
        }

        [Inline]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static int ReadSignal(int address, Signal signal)
        {
            return 0;
        }

        [Inline]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static void Write(int address, int value) { }
    }
}
