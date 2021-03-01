using FactoVision.Runtime.CompilerServices;

namespace FactoVision.Runtime
{
    public class NumericDisplay
    {
        private const int BaseAddress = 35841;

        private const int Display1Address = BaseAddress;
        private const int Display2Address = BaseAddress + 1;

        public static int Value
        {
            [Inline]
            get { return Memory.Read(Display1Address); }
            [Inline]
            set { Memory.Write(Display1Address, value); }
        }

        public static int Value2
        {
            [Inline]
            get { return Memory.Read(Display2Address); }
            [Inline]
            set { Memory.Write(Display2Address, value); }
        }
    }
}
