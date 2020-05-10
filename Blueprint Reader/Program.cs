using Microsoft.Extensions.Configuration;

namespace BlueprintReader
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            BlueprintReader.Run(configuration);
        }
    }
}
