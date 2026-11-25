namespace FlickerVoid.Editor.Persistence.Layout;

/// <summary>The editor's layout file: how the user arranged the main window's panels.</summary>
public sealed class EditorLayoutFile
{
    /// <summary>Whether the outliner is over the context panel instead of under it.</summary>
    public bool PanelsSwapped { get; set; }
}
