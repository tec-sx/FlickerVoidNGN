using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Facts;

/// <summary>Declares a fact with its default value, optional range and value labels.</summary>
public sealed class FactDefinition
{
    public Tag Tag { get; init; }

    /// <summary>Value written when the database is created or reset.</summary>
    public int DefaultValue { get; init; }

    /// <summary>Keeps the value between Min and Max.</summary>
    public bool Clamp { get; init; }

    public int Min { get; init; }

    public int Max { get; init; }

    /// <summary>Optional labels for enum-like facts; the index is the value.</summary>
    public List<string> ValueNames { get; init; } = [];

    public string Description { get; init; } = string.Empty;
}
