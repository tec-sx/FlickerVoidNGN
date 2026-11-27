namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>
/// A room of a storey: its name and a point inside it. Rooms are the spaces the walls close; the point says which space the name
/// belongs to, so a room keeps its name while its walls move.
/// </summary>
public sealed class PlanRoom
{
    public string Name { get; set; } = string.Empty;

    public float X { get; set; }

    public float Z { get; set; }

    /// <summary>The share of the mood's ambient light that reaches inside it.</summary>
    public float Ambient { get; set; } = 0.1f;
}
