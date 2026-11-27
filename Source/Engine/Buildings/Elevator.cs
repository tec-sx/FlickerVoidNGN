namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// An elevator's platform: how far above its rest it stands at each stop, bottom first, and the stop it is at or travels to. Using it
/// sends it to the next stop up, and from the top back to the bottom.
/// </summary>
public struct Elevator
{
    public Elevator()
    {
    }

    public float[] Stops { get; set; } = [];

    public int Stop { get; set; }
}
