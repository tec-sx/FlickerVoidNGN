namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>A pillar standing on a storey's floor up to its ceiling: square, or round, centred on its point.</summary>
public sealed class PlanPillar
{
    public int Id { get; set; }

    public float X { get; set; }

    public float Z { get; set; }

    /// <summary>Its width, or a round pillar's diameter.</summary>
    public float Size { get; set; } = 0.4f;

    public bool Round { get; set; }
}
