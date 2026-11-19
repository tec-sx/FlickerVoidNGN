using System.Text.Json;

namespace FlickerVoid.Engine.Debug;

/// <summary>The debug windows, grouped by category in the order they were added, and which of them are open.</summary>
public sealed class DebugWindows
{
    private readonly List<DebugWindow> _windows = [];

    public IReadOnlyList<DebugWindow> All => _windows;

    public IReadOnlyList<DebugCategory> Categories
    {
        get
        {
            List<DebugCategory> categories = [];
            foreach (IGrouping<string, DebugWindow> group in _windows.GroupBy(window => window.Category, StringComparer.OrdinalIgnoreCase))
            {
                categories.Add(new DebugCategory(group.Key, [.. group]));
            }

            return categories;
        }
    }

    /// <exception cref="ArgumentException">A window with the same title, ignoring case, is already added.</exception>
    public void Add(DebugWindow window)
    {
        DebugWindow? existing = Find(window.Title);
        if (existing is not null)
        {
            throw new ArgumentException($"A debug window called {window.Title} is already added.", nameof(window));
        }

        _windows.Add(window);
    }

    public DebugWindow? Find(string title)
    {
        return _windows.Find(window => string.Equals(window.Title, title, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The open windows' titles as JSON.</summary>
    public string SaveLayout()
    {
        DebugLayout layout = new() { OpenWindows = [.. _windows.Where(window => window.IsOpen).Select(window => window.Title)] };
        return JsonSerializer.Serialize(layout, DebugJsonContext.Default.DebugLayout);
    }

    /// <summary>Opens the windows the layout lists and closes the rest; saved windows that no longer exist are skipped, and text that isn't a layout changes nothing.</summary>
    public void LoadLayout(string json)
    {
        DebugLayout? layout;
        try
        {
            layout = JsonSerializer.Deserialize(json, DebugJsonContext.Default.DebugLayout);
        }
        catch (JsonException)
        {
            return;
        }

        if (layout is null)
        {
            return;
        }

        foreach (DebugWindow window in _windows)
        {
            window.IsOpen = layout.OpenWindows.Contains(window.Title, StringComparer.OrdinalIgnoreCase);
        }
    }
}
