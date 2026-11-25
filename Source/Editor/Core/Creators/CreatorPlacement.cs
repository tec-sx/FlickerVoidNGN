namespace FlickerVoid.Editor.Core.Creators;

/// <summary>Where the creator makes a creation: on one placed in the level, or alone, on a copy of its file.</summary>
public enum CreatorPlacement
{
    /// <summary>On a creation placed in the level, with the rest of the level drawn black and white.</summary>
    InPlace,

    /// <summary>On a temporary copy of a file that is never saved, with the level set aside.</summary>
    Alone,
}
