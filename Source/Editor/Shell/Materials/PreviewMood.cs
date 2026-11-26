namespace FlickerVoid.Editor.Materials;

/// <summary>How the material editor's preview is lit.</summary>
public enum PreviewMood
{
    /// <summary>A sun from the upper left in front and soft ambient light.</summary>
    Day,

    /// <summary>Faint blue light and a warm lamp beside the shape.</summary>
    Night,

    /// <summary>A dim room lit by a warm lamp above the shape, without the sun.</summary>
    Indoors,
}
