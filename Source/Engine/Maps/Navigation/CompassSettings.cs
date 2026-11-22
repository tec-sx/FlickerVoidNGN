namespace FlickerVoid.Engine.Maps.Navigation;

/// <summary>How much of the world a navigator's compass covers.</summary>
public sealed class CompassSettings
{
    /// <summary>Degrees of bearing the compass shows, half either side of the view.</summary>
    public float FieldOfView { get; set; } = 180;
}
