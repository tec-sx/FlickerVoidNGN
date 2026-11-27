namespace FlickerVoid.Engine.Audio.Music;

/// <summary>Where in its loop a track begins when it comes in.</summary>
public enum MusicPhase
{
    /// <summary>Where the loop would be had it played since the cue began, so every loop stays in step with the others.</summary>
    Locked,

    /// <summary>At the loop's start.</summary>
    FromStart,
}
