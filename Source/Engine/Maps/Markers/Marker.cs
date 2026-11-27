using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Localization;

namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>A marker the map system holds: on an entity through its <see cref="MapMarker"/>, or at a place.</summary>
internal sealed class Marker(MarkerHandle handle, MarkerDefinition definition)
{
    public MarkerHandle Handle { get; } = handle;

    public MarkerDefinition Definition { get; } = definition;

    public Entity Entity { get; init; } = Entity.Null;

    public Vector3 Location { get; init; }

    public LocalizedText Label { get; init; } = new();

    public Gameplay.Tags.Tag DiscoveredFact { get; init; }

    public bool Discovered { get; set; } = true;

    public bool Visible { get; set; } = true;
}
