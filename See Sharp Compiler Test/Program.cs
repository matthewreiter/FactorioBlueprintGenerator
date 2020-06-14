using SeeSharp.Runtime;

namespace SeeSharpCompilerTest
{
    public static class Program
    {
        private static int counter = -50;
        private static Stuff moreStuff = new Stuff { Field1 = -10, Field2 = -20, Things1 = new Things { Type = -30, Count = -40 } };
        private static readonly int[] soManyInts = { 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40, 42 };
        private static readonly int[] someMoreInts = { -2, -4, -6 };
        private static readonly Things[] someThings = new Things[] { new Things { Type = 67, Count = 89 }, new Things { }, new Things() };
        private static readonly char[] stringish = new char[] { 'H', 'e', 'l', 'l', 'o' };
        private static readonly string anActualString = "Hello";
        private static readonly int[] blank = new int[4];

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
            soManyInts[5]++;
            soManyInts[7] = soManyInts[8] - 1;

            var stuff = new Stuff { Field1 = 32, Field2 = 64 };
            stuff.Things1 = new Things { Type = 123, Count = 45 };
            stuff.Field2 += stuff.Field1;

            var fieldSum = GetFieldSum(stuff);

            var copyOfThings = stuff.Things1;
            someThings[1] = copyOfThings;
            someThings[2] = someThings[1];

            object maybeString = anActualString;
            var isString = maybeString is string;
            var isChar = maybeString is char;

            //var helloGoodbye = anActualString + "Goodbye";

            for (int index = 0; index < 4; index++)
            {
                sum *= 2;
                counter++;
            }
        }

        private static int GetFieldSum(Stuff stuff)
        {
            return stuff.Field1 + stuff.Field2;
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

        private struct Stuff
        {
            public int Field1;
            public int Field2;
            public Things Things1;
        }

        private struct Things
        {
            public int Type;
            public int Count;
        }
    }
}
