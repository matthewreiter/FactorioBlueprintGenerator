using Microsoft.Extensions.Configuration;

namespace MusicBoxCompiler
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            MusicBoxCompiler.Run(configuration);
        }
    }
}
