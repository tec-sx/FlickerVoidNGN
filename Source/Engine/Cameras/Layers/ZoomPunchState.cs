namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>The running state of a zoom punch: its strength and how long since it began; none runs at first.</summary>
public sealed class ZoomPunchState
{
    public float Strength { get; set; }

    public float Time { get; set; } = float.PositiveInfinity;
}
