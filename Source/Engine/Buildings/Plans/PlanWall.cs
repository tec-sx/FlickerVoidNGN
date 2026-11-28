using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>
/// A wall of a storey's plan from one corner to another, standing on the floor: straight, or bent into an arc by its bulge. An outside
/// wall is part of the shell the floor is laid under; an inside wall divides it into rooms.
/// </summary>
public sealed class PlanWall
{
    public int Id { get; set; }

    /// <summary>The id of the corner it starts at.</summary>
    public int From { get; set; }

    /// <summary>The id of the corner it ends at.</summary>
    public int To { get; set; }

    public bool Outside { get; set; }

    /// <summary>How thick it is; null takes the storey's wall thickness.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Thickness { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public WallHeight Height { get; set; }

    /// <summary>How high a wall of <see cref="WallHeight.Custom"/> height stands above the floor.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float CustomHeight { get; set; }

    /// <summary>How far the middle of an arc stands off the straight line between its corners, to the left walking from start to end; 0 is straight.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float Bulge { get; set; }

    /// <summary>How many straight pieces an arc is made of.</summary>
    public int Segments { get; set; } = 10;
}
