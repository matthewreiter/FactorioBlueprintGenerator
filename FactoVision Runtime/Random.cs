namespace FactoVision.Runtime
{
    public class Random
    {
        private const int A = 1103515245;
        private const int C = 12345;

        private int Seed = Clock.AbsoluteTicks;

        public int Next()
        {
            // Generates a random number using the linear congruential method
            Seed = A * Seed + C;
            return Seed & 0x3FFFFFFF;
        }

        public int Next(int maxValue)
        {
            return Next() % maxValue;
        }
    }
}
