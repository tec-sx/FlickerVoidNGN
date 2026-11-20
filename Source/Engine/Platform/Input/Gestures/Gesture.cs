namespace FlickerVoid.Engine.Platform.Input.Gestures;

/// <summary>What an input must do to complete, such as hold for a second or press three times.</summary>
public sealed record Gesture
{
    public GestureMode Mode { get; set; } = GestureMode.Press;

    /// <summary>Seconds, for Hold, Mash and Hover.</summary>
    public float Duration { get; set; }

    /// <summary>Presses, for Mash.</summary>
    public int PressCount { get; set; } = 1;
}
