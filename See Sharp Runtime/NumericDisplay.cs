using SeeSharp.Runtime.CompilerServices;

namespace SeeSharp.Runtime
{
    public class NumericDisplay
    {
        private const int BaseAddress = 35841;

        public static int Value
        {
            [Inline]
            get { return Memory.Read(BaseAddress); }
            [Inline]
            set { Memory.Write(BaseAddress, value); }
        }
    }
}
