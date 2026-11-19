using FlickerVoid.Engine.Runtime.Debugging;

namespace FlickerVoid.Engine.Debug;

/// <summary>The lines typed into a debug console and what it answered, the latest <see cref="Capacity"/> of them.</summary>
public sealed class ConsoleLog
{
    public const int Capacity = 200;

    private readonly IDebugConsole _console;
    private readonly List<string> _lines = [];
    private readonly List<string> _history = [];

    public ConsoleLog(IDebugConsole console)
    {
        _console = console;
    }

    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Runs the line and logs it and the answer; blank lines do nothing.</summary>
    public void Submit(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        _history.Add(trimmed);
        _lines.Add($"> {trimmed}");
        string answer = _console.Execute(trimmed);
        if (answer.Length > 0)
        {
            _lines.AddRange(answer.Split('\n'));
        }

        if (_lines.Count > Capacity)
        {
            _lines.RemoveRange(0, _lines.Count - Capacity);
        }
    }

    /// <summary>The line typed <paramref name="back"/> lines ago (1 is the latest), or null when there are not that many.</summary>
    public string? Recall(int back)
    {
        int index = _history.Count - back;
        if (back < 1 || index < 0)
        {
            return null;
        }

        return _history[index];
    }
}
