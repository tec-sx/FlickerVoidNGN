namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>The running state of a shake: the strongest shake asked for, how long it lasts and how long it has left.</summary>
public sealed class ShakeState
{
    public float Strength { get; set; }

    public float Duration { get; set; }

    public float TimeLeft { get; set; }

    public float Time { get; set; }
}
