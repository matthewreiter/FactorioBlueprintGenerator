using Microsoft.Extensions.Configuration;

namespace FindAndReplace
{
    public class Program
    {
        static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            FindAndReplace.Run(configuration);
        }
    }
}
