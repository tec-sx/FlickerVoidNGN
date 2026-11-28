using FlickerVoid.Engine.Cameras.Layers;

namespace FlickerVoid.Engine.Cameras.Shots;

/// <summary>
/// A shot placed in a creation, such as a building's: its layers given in place of a rig file, and never the shot a scene starts on,
/// as only its creation's triggers go to it.
/// </summary>
public struct PlacedShot
{
    public PlacedShot()
    {
    }

    public CameraRig Rig { get; set; } = new();
}
