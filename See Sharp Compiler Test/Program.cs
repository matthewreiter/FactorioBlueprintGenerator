using SeeSharp.Runtime;

namespace SeeSharpCompilerTest
{
    public static class Program
    {
        private static int counter = -50;

        public static void Main()
        {
            DoNothing();
            IgnoreResult();
            var u235 = FactoryNetwork.GetValue(Signal.Uranium235);
            var a = 5;
            var b = 10;
            var c = ~b | a;
            var sum = a + b + u235 % 100;
            GetAnswer(out var answer);
            GetAnswer(out counter);
            var isFalse = !true;
            var isTrue = !isFalse || false;

            for (int index = 0; index <= 3; index++)
            {
                sum *= 2;
                counter++;
            }
        }

        private static void GetAnswer(out int result)
        {
            result = 42;
        }

        private static void DoNothing() { }

        private static int IgnoreResult()
        {
            return 100;
        }
    }
}
