using System;

namespace MusicBoxCompiler.Utils;

internal static class MathExtensions
{
    extension(Math)
    {
        public static TimeSpan Max(TimeSpan val1, TimeSpan val2) => val1 > val2 ? val1 : val2;
    }
}
