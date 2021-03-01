using Microsoft.Extensions.Configuration;

namespace FactoVision.Compiler
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            Assembler.Run(configuration);
        }
    }
}
