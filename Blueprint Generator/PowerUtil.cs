using BlueprintCommon.Constants;
using BlueprintCommon.Models;
using System;
using System.Collections.Generic;

namespace BlueprintGenerator
{
    public class PowerUtil
    {
        public static List<Entity> CreateSubstations(int substationWidth, int substationHeight, int xOffset, int yOffset, int baseEntityNumber, GridConnectivity connectivity = GridConnectivity.Full)
        {
            var substations = new List<Entity>();

            for (int row = 0; row < substationHeight; row++)
            {
                for (int column = 0; column < substationWidth; column++)
                {
                    var entityNumber = baseEntityNumber + row * substationWidth + column;
                    var neighbors = new List<int>();

                    if (row == 0 && connectivity.HasFlag(GridConnectivity.Top) ||
                        row == substationHeight - 1 && connectivity.HasFlag(GridConnectivity.Bottom) ||
                        connectivity.HasFlag(GridConnectivity.Horizontal))
                    {
                        if (column > 0)
                        {
                            neighbors.Add(entityNumber - 1);
                        }

                        if (column < substationWidth - 1)
                        {
                            neighbors.Add(entityNumber + 1);
                        }
                    }

                    if (column == 0 && connectivity.HasFlag(GridConnectivity.Left) ||
                        column == substationWidth - 1 && connectivity.HasFlag(GridConnectivity.Right) ||
                        connectivity.HasFlag(GridConnectivity.Vertical))
                    {
                        if (row > 0)
                        {
                            neighbors.Add(entityNumber - substationWidth);
                        }

                        if (row < substationHeight - 1)
                        {
                            neighbors.Add(entityNumber + substationWidth);
                        }
                    }

                    substations.Add(new Entity
                    {
                        Entity_number = entityNumber,
                        Name = ItemNames.Substation,
                        Position = new Position
                        {
                            X = column * 18 + 0.5 + xOffset,
                            Y = row * 18 + 0.5 + yOffset
                        },
                        Neighbors = neighbors
                    });
                }
            }

            return substations;
        }

        [Flags]
        public enum GridConnectivity
        {
            None = 0,
            Top = 1,
            Bottom = 2,
            Left = 4,
            Right = 8,
            Vertical = 16,
            Horizontal = 32,
            Full = Top | Bottom | Left | Right | Vertical | Horizontal
        }
    }
}
