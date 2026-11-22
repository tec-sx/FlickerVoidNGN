namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>How a request changes the live shot: replacing it, pushing it to return to later, or returning to the last pushed.</summary>
public enum ShotSwitchMode
{
    Switch,
    Push,
    Return,
}
