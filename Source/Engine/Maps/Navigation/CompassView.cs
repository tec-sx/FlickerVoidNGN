namespace FlickerVoid.Engine.Maps.Navigation;

/// <summary>What the compass shows: the view's heading and the markers within its field of view.</summary>
public sealed class CompassView
{
    /// <summary>Degrees clockwise from north, 0 to 360.</summary>
    public float Heading { get; init; }

    public List<MarkerView> Markers { get; } = [];
}
