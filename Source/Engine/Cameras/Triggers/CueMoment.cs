namespace FlickerVoid.Engine.Cameras.Triggers;

/// <summary>
/// When a camera cue fires: as an activator walks into its area, out of it, or both, going back on the way out from what it did on
/// the way in.
/// </summary>
public enum CueMoment
{
    Enter,
    Exit,
    Both,
}
