namespace FlickerVoid.Engine.Graphics.AccelerationStructures;

/// <summary>How many instances a top-level acceleration structure can hold.</summary>
public readonly record struct TopLevelDescription(int InstanceCapacity)
{
    /// <summary>Shown in debuggers and validation messages.</summary>
    public string? Name { get; init; }
}
