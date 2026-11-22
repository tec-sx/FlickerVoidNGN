using System.Numerics;

using FlickerVoid.Engine.Maps.Definitions;

namespace FlickerVoid.Engine.Maps.Navigation;

/// <summary>What the world map shows of one layer: where the navigator is on it and the markers on it.</summary>
public sealed class WorldMapView
{
    public MapDefinition? Map { get; init; }

    public int LayerIndex { get; init; } = -1;

    public bool PlayerOnLayer { get; init; }

    public Vector2 PlayerUv { get; init; }

    public float PlayerRotation { get; init; }

    public List<MarkerView> Markers { get; } = [];
}
