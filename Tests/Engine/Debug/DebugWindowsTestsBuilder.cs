namespace FlickerVoid.Engine.Debug.Tests;

/// <summary>Builds a window registry holding windows that draw nothing.</summary>
internal sealed class DebugWindowsTestsBuilder
{
    private readonly List<DebugWindow> _windows = [];

    public static DebugWindow CreateWindow(string title, string category, bool open = false)
    {
        return new ActionWindow(title, category, static () => { }) { IsOpen = open };
    }

    public DebugWindowsTestsBuilder WithWindow(string title, string category, bool open = false)
    {
        _windows.Add(CreateWindow(title, category, open));
        return this;
    }

    public DebugWindows Build()
    {
        DebugWindows windows = new();
        foreach (DebugWindow window in _windows)
        {
            windows.Add(window);
        }

        return windows;
    }
}
