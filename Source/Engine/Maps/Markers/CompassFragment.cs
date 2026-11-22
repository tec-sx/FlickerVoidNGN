namespace FlickerVoid.Engine.Maps.Markers;

/// <summary>Shows the marker on the compass.</summary>
public sealed class CompassFragment : MarkerFragment
{
    /// <summary>Metres beyond which it is left out; 0 shows it at any distance. Tracked markers ignore it.</summary>
    public float MaxDistance { get; set; }
}
