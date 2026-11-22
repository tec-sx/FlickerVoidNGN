namespace FlickerVoid.Engine.Maps.Navigation;

/// <summary>How much of the world a navigator's minimap shows, and how it turns.</summary>
public sealed class MinimapSettings
{
    /// <summary>Metres from the centre to the edge.</summary>
    public float Radius { get; set; } = 50;

    public float MinRadius { get; set; } = 15;

    public float MaxRadius { get; set; } = 200;

    /// <summary>Turns the map with the view so it points up; otherwise north stays up.</summary>
    public bool RotateWithView { get; set; } = true;

    /// <summary>In a room (a height-limited layer) the radius shrinks to the room's half size times this; 0 turns it off.</summary>
    public float RoomRadiusScale { get; set; } = 1.2f;
}
