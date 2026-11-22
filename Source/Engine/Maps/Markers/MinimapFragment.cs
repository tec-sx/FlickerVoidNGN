namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>Shows the marker on the minimap.</summary>
public sealed class MinimapFragment : MarkerFragment
{
    /// <summary>Pins it to the minimap's edge while it is out of range; tracked markers always are.</summary>
    public bool ClampToEdge { get; set; }

    /// <summary>Metres beyond which it is left out; 0 shows it at any distance.</summary>
    public float MaxDistance { get; set; }

    /// <summary>Turns the icon with its entity, such as a cart.</summary>
    public bool RotateWithEntity { get; set; }
}
