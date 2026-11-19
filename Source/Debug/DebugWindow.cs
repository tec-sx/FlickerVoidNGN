namespace FlickerVoid.Engine.Debug;

/// <summary>
/// A debug panel: a titled ImGui window listed in the main menu under its category, where it is opened and closed. Derive from it and
/// draw the contents in <see cref="Render"/> with Dear ImGui calls, then add it to <see cref="DebugUi.Windows"/>.
/// </summary>
public abstract class DebugWindow
{
    protected DebugWindow(string title, string category)
    {
        Title = title;
        Category = category;
    }

    /// <summary>Names the window; unique among windows, ignoring case.</summary>
    public string Title { get; }

    /// <summary>The main menu this window is listed under.</summary>
    public string Category { get; }

    public bool IsOpen { get; set; }

    /// <summary>Draws the contents inside the window while it is open.</summary>
    public abstract void Render();
}
