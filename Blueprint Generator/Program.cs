using Microsoft.Extensions.Configuration;

namespace BlueprintGenerator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            MemoryInitializer.Run(configuration);
        }
    }
}
