using System.Text.Json.Serialization;

using FlickerVoid.Engine.Creations;

namespace FlickerVoid.Engine.Buildings;

/// <summary>One storey of a building: its level (-1 the cellar, 0 the ground floor) and its shell, the floor and walls as one solid.</summary>
public sealed class BuildingStorey
{
    public int Level { get; set; }

    public required SolidPart Shell { get; set; }

    /// <summary>The share of the mood's ambient light that reaches inside it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float Ambient { get; set; } = 0.1f;
}
