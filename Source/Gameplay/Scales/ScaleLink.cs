namespace FlickerVoid.Engine.Gameplay.Scales;

/// <summary>Passes part of every change of one scale on to another.</summary>
public sealed class ScaleLink
{
    public required ScaleDefinition Scale { get; init; }

    /// <summary>Fraction of the applied change passed on; negative to move the other scale the opposite way.</summary>
    public float Factor { get; init; } = 1f;

    /// <summary>Pass on the size of the change whatever its direction, so a loss here still raises the other scale.</summary>
    public bool Magnitude { get; init; }
}
