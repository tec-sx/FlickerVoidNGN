namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>What saves keep of a shot the director has moved, by its entity's name: where it is on its timeline and where it plays to.</summary>
public sealed class SavedShot
{
    public string Shot { get; set; } = string.Empty;

    public float Time { get; set; }

    public float Target { get; set; }

    public bool Playing { get; set; }

    public bool LayersOff { get; set; }
}
