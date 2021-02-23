using FactoVision.Runtime;
using System.Threading;

namespace FactoVisionCompilerTest
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
        private static readonly Wrapper<int> wrapper = new Wrapper<int> { Value = 5 };
        private static readonly Wrapper<Things> thingWrapper = new Wrapper<Things> { Value = new Things { Count = 8, Type = 16 } };

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

            var custom = new Custom2(100, 200, 300, 400);

            custom.Field3 += custom.Field2;
            custom.Field5 = new Things { Count = 1, Type = 2 };
            custom.Field6 = custom.Field5;

            var wrappedValue = wrapper.Value;
            var wrappedThing = thingWrapper.Value;

            object maybeString = anActualString;
            var isString = maybeString is string;
            var isChar = maybeString is char;

            //var helloGoodbye = anActualString + "Goodbye";
            var strLen = anActualString.Length;
            Thread.Sleep(1000);

            for (int index = 0; index < 4; index++)
            {
                sum *= 2;
                counter++;
            }

            int? nullable = null;
            nullable = 7;
            if (nullable.HasValue)
            {
                var value = nullable.Value;
            }

            while (true)
            {
                for (int index = 0; index < 10; index++)
                {
                    Speaker.SetDrum(0, Speaker.Drum.Triangle, Speaker.Volume.Medium);
                    Speaker.PlayAndClear();

                    Speaker.SetChannel(Speaker.Instrument.Piano, 0, 10 + index);
                    Speaker.SetChannel(Speaker.Instrument.Piano, 1, 12 + index);
                    Speaker.Play();
                    Speaker.PlayAndClear();

                    Speaker.SetChannel(Speaker.Instrument.Piano, 0, 20 + index, Speaker.Volume.Low);
                    Speaker.PlayAndClear();
                    Speaker.SetChannel(Speaker.Instrument.Piano, 0, 20 + index, Speaker.Volume.Medium);
                    Speaker.PlayAndClear();
                    Speaker.SetChannel(Speaker.Instrument.Piano, 0, 20 + index, Speaker.Volume.High);
                    Speaker.PlayAndClear();
                    Speaker.SetChannel(Speaker.Instrument.Piano, 0, 20 + index, Speaker.Volume.Max);
                    Speaker.PlayAndClear();
                }
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

        private class Custom
        {
            public int Field1;
            public int Field2;

            public Custom(int field1, int field2)
            {
                Field1 = field1;
                Field2 = field2;
            }
        }

        private class Custom2 : Custom
        {
            public int Field3;
            public int Field4;
            public Things Field5;
            public Things Field6;

            public Custom2(int field1, int field2, int field3, int field4) : base(field1, field2)
            {
                Field3 = field3;
                Field4 = field4;
            }
        }

        private class Wrapper<T>
        {
            public T Value;
        }
    }
}
