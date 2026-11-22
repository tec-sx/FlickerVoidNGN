using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Maps.Navigation;

/// <summary>The game's map settings.</summary>
public sealed class MapSettings
{
    /// <summary>The marker of the waypoint the player places.</summary>
    [ContentFile(".fvdata")]
    public string WaypointMarker { get; set; } = string.Empty;

    /// <summary>Metres of height beyond which a marker reads as above or below the navigator.</summary>
    public float ElevationThreshold { get; set; } = 3;

    /// <summary>Seconds between the navigator's checks of the map underfoot and the places near it.</summary>
    public float UpdateInterval { get; set; } = 0.25f;
}
