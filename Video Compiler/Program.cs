using Microsoft.Extensions.Configuration;

namespace VideoCompiler
{
    public class Program
    {
        static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            VideoCompiler.Run(configuration);
        }
    }
}
