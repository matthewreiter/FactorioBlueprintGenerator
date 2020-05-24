using Microsoft.Extensions.Configuration;

namespace SeeSharpCompiler
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            SeeSharpCompiler.Run(configuration);
        }
    }
}
