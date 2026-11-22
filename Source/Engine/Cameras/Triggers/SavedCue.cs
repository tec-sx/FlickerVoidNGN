namespace FlickerVoid.Engine.Cameras.Triggers;

/// <summary>What saves keep of a saveable camera cue: whether it already fired.</summary>
public sealed class SavedCue
{
    public bool Fired { get; set; }
}
