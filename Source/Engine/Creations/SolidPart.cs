using System.Numerics;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Creations;

/// <summary>
/// One part of a creation, as its file holds it: where its pivot stands in the creation's space, and its solid mesh in the pivot's space. A door also has a
/// joint that moves it and the action that opens and closes it.
/// </summary>
public sealed class SolidPart
{
    public Transform Place { get; set; } = new();

    public required SolidMesh Mesh { get; set; }

    /// <summary>
    /// Whether the creator edits the mesh the same on both sides of the part's centre line (its X = 0 plane); the switch is kept
    /// here so the file opens with it as it was left.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Mirror { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PartJoint? Joint { get; set; }

    /// <summary>The offer action that toggles the part's joint open and shut, such as <c>Interaction.Action.Toggle</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Tag? Toggle { get; set; }

    /// <summary>The middle of the box around the part's mesh, in the pivot's space: where a door is reached.</summary>
    public Vector3 GetMiddle()
    {
        if (Mesh.Points.Count == 0)
        {
            return Vector3.Zero;
        }

        Vector3 low = Mesh.Points[0];
        Vector3 high = low;
        foreach (Vector3 point in Mesh.Points)
        {
            low = Vector3.Min(low, point);
            high = Vector3.Max(high, point);
        }

        return (low + high) / 2;
    }
}
