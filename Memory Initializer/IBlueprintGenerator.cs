using BlueprintCommon.Models;
using Microsoft.Extensions.Configuration;

namespace MemoryInitializer
{
    public interface IBlueprintGenerator
    {
        public Blueprint Generate(IConfigurationRoot configuration);
    }
}
