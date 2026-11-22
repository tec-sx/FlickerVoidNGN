namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>Shows the marker on the world map.</summary>
public sealed class WorldMapFragment : MarkerFragment
{
    /// <summary>Hidden below this world map zoom, so minor places appear as the player zooms in.</summary>
    public float MinZoom { get; set; }

    public bool RotateWithEntity { get; set; }
}
