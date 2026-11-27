using System.Text.Json.Serialization;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>
/// An opening of a storey: a door, doorway or window cut through a wall at a distance along it and a lift above the floor, or a hole
/// in the floor at a point. A door's leaf swings on its hinge and can be locked.
/// </summary>
public sealed class PlanOpening
{
    public int Id { get; set; }

    /// <summary>What it is called in the editor's parts: Front door, Window 2.</summary>
    public string Name { get; set; } = string.Empty;

    public OpeningKind Kind { get; set; }

    /// <summary>The id of the wall it is cut through; unused for a hole in the floor.</summary>
    public int Wall { get; set; }

    /// <summary>How far its middle is from the wall's start, along the wall, in metres.</summary>
    public float Along { get; set; }

    /// <summary>How high its foot is above the floor.</summary>
    public float Lift { get; set; }

    /// <summary>A hole in the floor's middle, across the floor (X).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float X { get; set; }

    /// <summary>A hole in the floor's middle, across the floor (Z).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float Z { get; set; }

    public float Width { get; set; } = 1;

    /// <summary>How high it is, its arch among it; a hole's depth across the floor.</summary>
    public float Height { get; set; } = 2.1f;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public OpeningTop Top { get; set; }

    /// <summary>How high an arched top rises above its sides.</summary>
    public float ArchRise { get; set; } = 0.5f;

    /// <summary>How thick a door's leaf is.</summary>
    public float Thickness { get; set; } = 0.05f;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HingeLeft { get; set; }

    /// <summary>Whether the leaf swings out of the wall's outer side (the right of the wall walking from its start to its end).</summary>
    public bool Outward { get; set; } = true;

    /// <summary>How far the leaf swings open, in degrees.</summary>
    public float Angle { get; set; } = 90;

    /// <summary>How wide the frame round it is; 0 has none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float Frame { get; set; }

    /// <summary>Whether a window has a pane of glass.</summary>
    public bool Glass { get; set; } = true;

    /// <summary>How much larger than the leaf the cut is on every side.</summary>
    public float Margin { get; set; } = 0.02f;

    /// <summary>Whether the door starts locked.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Locked { get; set; }

    /// <summary>The tag an item must have to open the lock; none when no key fits.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Tag Key { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ConsumeKey { get; set; }

    /// <summary>Whether the lock can be picked.</summary>
    public bool Pickable { get; set; } = true;

    /// <summary>How hard the lock is to pick, from 0 to 1.</summary>
    public float Difficulty { get; set; } = 0.5f;

    /// <summary>A copy to change without touching this one.</summary>
    public PlanOpening Copy() => (PlanOpening)MemberwiseClone();
}
