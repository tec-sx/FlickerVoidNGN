using System.Text.Json.Serialization;

using FlickerVoid.Engine.Buildings.Plans;
using FlickerVoid.Engine.Creations;

namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// One storey of a building: its level (-1 the cellar, 0 the ground floor), its height from its floor's underside to the next
/// storey's, and its plan of corners, walls, rooms, pillars and openings, from which the creator makes its parts: the floor, the
/// outside walls, each inside wall, pillar and door leaf, and each window's glass.
/// </summary>
public sealed class BuildingStorey
{
    public int Level { get; set; }

    /// <summary>How high it is, from the underside of its floor to the underside of the next storey's.</summary>
    public float Height { get; set; } = 3;

    /// <summary>How thick its walls are, unless a wall has its own thickness.</summary>
    public float Wall { get; set; } = 0.3f;

    /// <summary>How thick its floor is.</summary>
    public float Floor { get; set; } = 0.2f;

    /// <summary>The share of the mood's ambient light that reaches inside it, outside its rooms.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float Ambient { get; set; } = 0.1f;

    public List<PlanCorner> Corners { get; set; } = [];

    public List<PlanWall> Walls { get; set; } = [];

    public List<PlanRoom> Rooms { get; set; } = [];

    public List<PlanPillar> Pillars { get; set; } = [];

    public List<PlanOpening> Openings { get; set; } = [];

    /// <summary>Whether a pillar stands at every corner of the outside walls.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CornerPillars { get; set; }

    /// <summary>How wide the corner pillars are.</summary>
    public float CornerPillarSize { get; set; } = 0.5f;

    /// <summary>
    /// The parts the creator made from the plan, by their id within the storey: <c>floor</c>, <c>walls</c>, <c>wall.3</c>,
    /// <c>pillar.1</c>, <c>corner.2</c>, <c>door.4</c>, <c>glass.5</c>.
    /// </summary>
    public Dictionary<string, SolidPart> Parts { get; set; } = [];

    /// <summary>The parts reshaped by hand with the mesh tools, which the plan no longer makes again.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Free { get; set; }

    /// <summary>A storey made before plans: its floor and walls as one solid. The creator makes a plan in its place.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SolidPart? Shell { get; set; }

    /// <summary>Whether the part with the local id was reshaped by hand.</summary>
    public bool IsFree(string id) => Free?.Contains(id) == true;

    /// <summary>The next id free for a corner, wall, pillar or opening of this storey.</summary>
    public int NextId()
    {
        int highest = 0;
        foreach (PlanCorner corner in Corners)
        {
            highest = Math.Max(highest, corner.Id);
        }

        foreach (PlanWall wall in Walls)
        {
            highest = Math.Max(highest, wall.Id);
        }

        foreach (PlanPillar pillar in Pillars)
        {
            highest = Math.Max(highest, pillar.Id);
        }

        foreach (PlanOpening opening in Openings)
        {
            highest = Math.Max(highest, opening.Id);
        }

        return highest + 1;
    }
}
