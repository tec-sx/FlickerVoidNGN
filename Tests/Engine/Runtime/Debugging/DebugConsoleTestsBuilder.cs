using FlickerVoid.Engine.Runtime.Debugging;

namespace FlickerVoid.Engine.Runtime.Tests.Debugging;

/// <summary>Builds the debug console under test with its commands and variables.</summary>
internal sealed class DebugConsoleTestsBuilder
{
    private readonly List<ConsoleEntry> _entries = [];

    public DebugConsoleTestsBuilder WithEntry(ConsoleEntry entry)
    {
        _entries.Add(entry);
        return this;
    }

    public DebugConsole Build()
    {
        DebugConsole console = new();
        foreach (ConsoleEntry entry in _entries)
        {
            console.Add(entry);
        }

        return console;
    }
}
