using Microsoft.Extensions.Configuration;
using System.Text;

namespace MusicBoxCompiler
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // Required for reading from Excel spreadsheets from .NET Core apps

            var configuration = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            MusicBoxCompiler.Run(configuration);
        }
    }
}
