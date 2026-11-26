using System.Windows.Input;

namespace FlickerVoid.Editor.Documents;

/// <summary>
/// An editor opened in place as a tab over the viewport: its file (the key), its title, its view, what closing it does and, when it
/// saves on its own, what Ctrl+S runs while it is shown.
/// </summary>
public sealed class EditorDocument(string key, string title, object content, Action closed, ICommand? save = null)
{
    /// <summary>What makes it one tab: the file it edits, or the editor's name when it edits many.</summary>
    public string Key { get; } = key;

    public string Title { get; } = title;

    /// <summary>The view shown in place of the viewport while the tab is selected.</summary>
    public object Content { get; } = content;

    /// <summary>Runs once when the tab is closed.</summary>
    public Action Closed { get; } = closed;

    /// <summary>Saves it, or null when its edits are saved with the assets (Ctrl+Shift+S).</summary>
    public ICommand? Save { get; } = save;
}
