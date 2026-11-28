using System.Text.Json.Serialization;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Buildings;

/// <summary>The roof over the top storey: its shape, how high its slopes rise, how far it reaches past the walls, and how thick it is.</summary>
public sealed class BuildingRoof
{
    private Transform? _oldPlace;

    public RoofKind Kind { get; set; }

    /// <summary>How high a shed's or gable's slope rises above the eaves.</summary>
    public float Rise { get; set; } = 1.5f;

    /// <summary>How far it reaches past the outside walls.</summary>
    public float Overhang { get; set; } = 0.2f;

    public float Thickness { get; set; } = 0.2f;

    /// <summary>Whether a gable's ridge, or a shed's slope, runs along the building's depth (Z) instead of its width (X).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AlongDepth { get; set; }

    /// <summary>Whether it is left off, so the top storey stays open.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool None { get; set; }

    /// <summary>The roof's solid, which the creator makes.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SolidPart? Part { get; set; }

    /// <summary>Where the solid stands, as files written before roofs had shapes kept it beside the shape; read into <see cref="Part"/>.</summary>
    [JsonPropertyName("place")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Transform? OldPlace
    {
        get => null;
        set
        {
            _oldPlace = value;
            if (Part is not null && value is { } place)
            {
                Part.Place = place;
            }
        }
    }

    /// <summary>The solid, as files written before roofs had shapes kept it; read into <see cref="Part"/>.</summary>
    [JsonPropertyName("mesh")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SolidMesh? OldMesh
    {
        get => null;
        set
        {
            if (value is not null)
            {
                Part = new SolidPart { Place = _oldPlace ?? new Transform(), Mesh = value };
            }
        }
    }
}
