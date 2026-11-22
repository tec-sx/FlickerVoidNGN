using System.Numerics;

using FlickerVoid.Engine.Maps.Markers;

namespace FlickerVoid.Engine.Maps.Navigation;

/// <summary>
/// A marker as one view shows it. On the minimap <see cref="Position"/> is the offset from the centre in radii (+X right, +Y down);
/// on the world map it is where it lies on the layer's image; on the compass X runs from -1 (left edge) to 1 (right edge) and
/// <see cref="Rotation"/> is its bearing against the view.
/// </summary>
public sealed record MarkerView
{
    public MarkerHandle Handle { get; init; }

    public MarkerDefinition? Definition { get; init; }

    public string Label { get; init; } = string.Empty;

    public Vector2 Position { get; init; }

    /// <summary>Degrees clockwise from up.</summary>
    public float Rotation { get; init; }

    /// <summary>Metres from the navigator.</summary>
    public float Distance { get; init; }

    public MarkerElevation Elevation { get; init; }

    /// <summary>Out of range and pinned to the view's edge.</summary>
    public bool Clamped { get; init; }

    public bool Discovered { get; init; } = true;

    public bool Tracked { get; init; }
}
