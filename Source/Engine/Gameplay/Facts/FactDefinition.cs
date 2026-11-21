using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Facts;

/// <summary>Declares a fact with its default value, optional range and value labels.</summary>
public sealed class FactDefinition
{
    public Tag Tag { get; set; }

    /// <summary>Value written when the database is created or reset.</summary>
    public int DefaultValue { get; set; }

    /// <summary>Keeps the value between Min and Max.</summary>
    public bool Clamp { get; set; }

    public int Min { get; set; }

    public int Max { get; set; }

    /// <summary>Optional labels for enum-like facts; the index is the value.</summary>
    public List<string> ValueNames { get; set; } = [];

    public string Description { get; set; } = string.Empty;
}
