using BlueprintCommon.Models;

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

public enum ConnectionType
{
    Red1 = 1,
    Green1 = 2,
    Red2 = 3,
    Green2 = 4,
    Copper1 = 5,
    Copper2 = 6
}
