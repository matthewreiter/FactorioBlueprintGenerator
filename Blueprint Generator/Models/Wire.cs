using BlueprintCommon.Models;
using System.Collections.Generic;
using System.Linq;

namespace BlueprintGenerator.Models;

public record Wire((Entity Entity, ConnectionType Type) Connection1, (Entity Entity, ConnectionType Type) Connection2)
{
    public int[] ToArray()
    {
        return
        [
            Connection1.Entity.Entity_number,
            (int)Connection1.Type,
            Connection2.Entity.Entity_number,
            (int)Connection2.Type
        ];
    }
}

public static class WireExtensions
{
    public static List<int[]> ToArrayList(this List<Wire> wires) => [.. wires.Select(wire => wire.ToArray())];
}

public enum ConnectionType
{
    /// <summary>
    /// The red connection for most entities or the red input for combinators that have separate inputs and outputs.
    /// </summary>
    Red1 = 1,

    /// <summary>
    /// The green connection for most entities or the green input for combinators that have separate inputs and outputs.
    /// </summary>
    Green1 = 2,

    /// <summary>
    /// The red output for combinators that have separate inputs and outputs.
    /// </summary>
    Red2 = 3,

    /// <summary>
    /// The green output for combinators that have separate inputs and outputs.
    /// </summary>
    Green2 = 4,

    /// <summary>
    /// The copper wire connection for power poles or the first copper wire connection for power switches.
    /// </summary>
    Copper1 = 5,

    /// <summary>
    /// The second copper wire connection for power switches.
    /// </summary>
    Copper2 = 6
}
