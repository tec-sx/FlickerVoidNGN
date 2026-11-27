namespace FlickerVoid.Editor.Core.Creators;

/// <summary>Where the creator makes a creation: on one placed in the level, alone on a copy of its file, or on a greybox of the level.</summary>
public enum CreatorPlacement
{
    /// <summary>On a creation placed in the level, with the rest of the level drawn black and white.</summary>
    InPlace,

    /// <summary>On a temporary copy of a file that is never saved, with the level set aside.</summary>
    Alone,

    /// <summary>On a greybox of the level itself, shaped in place: its mesh is kept in the level, and undo and save are the level's.</summary>
    Piece,
}
