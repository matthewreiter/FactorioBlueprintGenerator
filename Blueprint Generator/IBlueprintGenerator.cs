using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;

namespace BlueprintGenerator
{
    public interface IBlueprintGenerator
    {
        public Blueprint Generate(IConfigurationRoot configuration);
    }
}
