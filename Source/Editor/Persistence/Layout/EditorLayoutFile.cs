namespace FlickerVoid.Editor.Persistence.Layout;

/// <summary>The editor's layout file: how the user arranged the main window's panels.</summary>
public sealed class EditorLayoutFile
{
    /// <summary>Whether the outliner is over the context panel instead of under it.</summary>
    public bool PanelsSwapped { get; set; }

    /// <summary>Whether the content drawer is open.</summary>
    public bool ContentOpen { get; set; }

    /// <summary>The content drawer's height.</summary>
    public double ContentHeight { get; set; } = EditorLayout.DefaultContentHeight;

    /// <summary>The Day dock's height.</summary>
    public double DayDockHeight { get; set; } = EditorLayout.DefaultDayDockHeight;

    /// <summary>The side column's width.</summary>
    public double SideWidth { get; set; } = EditorLayout.DefaultSideWidth;
}
