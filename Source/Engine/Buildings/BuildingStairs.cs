using System.Text.Json.Serialization;

using FlickerVoid.Engine.Creations;

namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// Stairs from a storey's floor up to the next one's: where their foot stands and which way they climb, how they run (straight, L or
/// U), their width, the run before the landing and the landing, and their steps. The floor above is opened over them, with a railing
/// round the opening.
/// </summary>
public sealed class BuildingStairs
{
    public int Id { get; set; }

    /// <summary>The level of the storey they climb from.</summary>
    public int Level { get; set; }

    /// <summary>Where the middle of the first step's front stands, across the floor (X).</summary>
    public float X { get; set; }

    /// <summary>Where the middle of the first step's front stands, across the floor (Z).</summary>
    public float Z { get; set; }

    /// <summary>The way they climb, in degrees turned from +Z toward +X.</summary>
    public float Heading { get; set; }

    public StairsKind Kind { get; set; }

    /// <summary>Whether an L or U turns to the left of someone climbing.</summary>
    public bool TurnLeft { get; set; } = true;

    public float Width { get; set; } = 1.2f;

    /// <summary>How far the first run goes along the floor before the landing; the whole run of straight stairs is set by their steps.</summary>
    public float FirstRun { get; set; } = 2.8f;

    /// <summary>How deep the landing is.</summary>
    public float Landing { get; set; } = 1.2f;

    /// <summary>The most a step may rise; the steps share the height between the floors evenly.</summary>
    public float Rise { get; set; } = 0.15f;

    /// <summary>How deep each step is.</summary>
    public float Tread { get; set; } = 0.28f;

    /// <summary>Whether the floor above is the last step, so the stairs end a tread before it.</summary>
    public bool MeetsFloor { get; set; } = true;

    /// <summary>How far the opening in the floor above reaches past the stairs on every side.</summary>
    public float Margin { get; set; } = 0.1f;

    /// <summary>The room a climber needs over a step; the floor above is opened wherever it would be lower.</summary>
    public float Headroom { get; set; } = 2.1f;

    /// <summary>Whether a railing stands round the opening above, but for the side the stairs come up.</summary>
    public bool Railing { get; set; } = true;

    /// <summary>The steps' solid, which the creator makes.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SolidPart? Part { get; set; }

    /// <summary>The railing's solid, which the creator makes.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SolidPart? RailingPart { get; set; }

    /// <summary>A copy to change without touching these stairs.</summary>
    public BuildingStairs Copy() => (BuildingStairs)MemberwiseClone();
}
