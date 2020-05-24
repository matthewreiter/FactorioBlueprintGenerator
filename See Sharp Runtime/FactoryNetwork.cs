using SeeSharp.Runtime.Attributes;

namespace SeeSharp.Runtime
{
    public static class FactoryNetwork
    {
        private const int Address = 32769;

        [Inline]
        public static int GetValue(Signal signal)
        {
            return Memory.ReadSignal(Address, signal);
        }
    }
}
