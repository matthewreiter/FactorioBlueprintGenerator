using BlueprintCommon.Models;
using System.Collections.Generic;
using System.Linq;

namespace MemoryInitializer
{
    public static class ConnectionUtil
    {
        public static Dictionary<string, ConnectionPoint> CreateConnections(params ConnectionPoint[] connectionPoints)
        {
            return connectionPoints
                .Select((connectionPoint, index) => new { Key = (index + 1).ToString(), Value = connectionPoint })
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }
}
