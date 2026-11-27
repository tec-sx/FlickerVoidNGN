namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>A point of a storey's plan where walls start and end, across the floor in the building's space (X and Z, in metres).</summary>
public sealed class PlanCorner
{
    public int Id { get; set; }

    public float X { get; set; }

    public float Z { get; set; }
}
