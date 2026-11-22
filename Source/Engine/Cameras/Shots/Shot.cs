namespace FlickerVoid.Engine.Cameras.Shots;

/// <summary>
/// A directed camera: its entity's <c>camera</c> gives the lens and its keys a rail and a timeline in the entity's space. A shot
/// without keys is still at its entity's transform.
/// </summary>
public struct Shot
{
    public Shot()
    {
    }

    public List<ShotKey> Keys { get; set; } = [];

    public ShotDrive Drive { get; set; }

    /// <summary>Whether the scene starts on this shot; without one, the first shot found starts.</summary>
    public bool Start { get; set; }
}
