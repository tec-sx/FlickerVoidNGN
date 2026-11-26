namespace FlickerVoid.Editor.Core.Meshes.Doors;

/// <summary>What the door tool makes: how thick the door is, how far it opens, which way, and whether its opening goes through the part.</summary>
public readonly record struct DoorOptions
{
    public DoorOptions()
    {
    }

    /// <summary>The door's depth into the part, and the pocket it is set in, in metres.</summary>
    public float Thickness { get; init; } = 0.1f;

    /// <summary>How far it swings open, in degrees, 1 to 170.</summary>
    public float Angle { get; init; } = 90;

    /// <summary>Whether it swings out of the part's face, or into it.</summary>
    public bool Outward { get; init; } = true;

    /// <summary>Whether the opening is cut through the whole part (needs Manifold's library), as for a hollow body, rather than a pocket the door's depth.</summary>
    public bool Through { get; init; }
}
