using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using System.Collections.Generic;
using System.Linq;

namespace MemoryInitializer
{
    public static class MemoryUtil
    {
        public static IEnumerable<Entity> CreateSubstations(int substationWidth, int substationHeight, int xOffset, int yOffset, int baseEntityNumber)
        {
            for (int row = 0; row < substationHeight; row++)
            {
                for (int column = 0; column < substationWidth; column++)
                {
                    yield return new Entity
                    {
                        Entity_number = baseEntityNumber + row * substationWidth + column,
                        Name = ItemNames.Substation,
                        Position = new Position
                        {
                            X = column * 18 + 0.5 + xOffset,
                            Y = row * 18 + 2.5 + yOffset
                        }
                    };
                }
            }
        }

        public static Dictionary<string, ConnectionPoint> CreateConnections(params ConnectionPoint[] connectionPoints)
        {
            return connectionPoints
                .Select((connectionPoint, index) => new { Key = (index + 1).ToString(), Value = connectionPoint })
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }
}
