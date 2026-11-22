using System.Numerics;

using FlickerVoid.Engine.Maps.Definitions;

namespace FlickerVoid.Engine.Maps.Navigation;

/// <summary>What the minimap shows: the layer under the navigator and the part of its image around it, how it turns, and its markers.</summary>
public sealed class MinimapView
{
    /// <summary>The map under the navigator, or null outside every map.</summary>
    public MapDefinition? Map { get; init; }

    public int LayerIndex { get; init; } = -1;

    public Vector2 CenterUv { get; set; } = new(0.5f);

    public Vector2 ExtentUv { get; set; } = new(0.5f);

    /// <summary>The layer drawn under a room's, such as the street around an inn; the same layer outdoors.</summary>
    public int BaseLayerIndex { get; set; } = -1;

    public Vector2 BaseCenterUv { get; set; } = new(0.5f);

    public Vector2 BaseExtentUv { get; set; } = new(0.5f);

    /// <summary>Metres from the centre to the edge.</summary>
    public float Radius { get; init; }

    /// <summary>Degrees clockwise to turn the map's image.</summary>
    public float MapRotation { get; init; }

    /// <summary>Degrees clockwise from up of the navigator's icon.</summary>
    public float PlayerRotation { get; init; }

    public List<MarkerView> Markers { get; } = [];
}
