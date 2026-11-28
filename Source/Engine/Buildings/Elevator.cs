namespace FlickerVoid.Engine.Buildings;

/// <summary>
/// An elevator's platform: how far above its rest it stands at each stop, bottom first, the level of each stop, the stop it is at or
/// travels to, and which way it last went. Riding it takes it a stop on the way it goes, turning at the top and the bottom.
/// </summary>
public struct Elevator
{
    public Elevator()
    {
    }

    public float[] Stops { get; set; } = [];

    public int[] Levels { get; set; } = [];

    public int Stop { get; set; }

    /// <summary>Whether it goes down on its next ride; it goes up first.</summary>
    public bool Falling { get; set; }
}
