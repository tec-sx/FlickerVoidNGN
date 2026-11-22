using System.Numerics;

namespace FlickerVoid.Engine.Maps.Navigation;

/// <summary>What a save keeps of the player's navigation: the waypoint, and the tracked marker (the waypoint, or a saveable entity's).</summary>
public sealed class SavedNavigation
{
    public Vector3? Waypoint { get; set; }

    public bool TrackingWaypoint { get; set; }

    /// <summary>The save id of the entity whose marker is tracked; empty for none.</summary>
    public string Tracked { get; set; } = string.Empty;
}
