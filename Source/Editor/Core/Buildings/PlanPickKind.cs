namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>What of a building is picked in its creator.</summary>
public enum PlanPickKind
{
    Storey,
    Wall,
    Corner,
    Room,
    Pillar,
    Opening,

    /// <summary>A door's handle: where a hand takes it.</summary>
    Handle,
    Stairs,
    Elevator,
    Shot,
    Roof,
}
