using FlickerVoid.Engine.Runtime.Debugging;

namespace FlickerVoid.Engine.Runtime.Tests.Debugging;

/// <summary>Builds the terminal console under test, reading the given typed lines and writing to <see cref="Output"/>.</summary>
internal sealed class TerminalConsoleSystemTestsBuilder(IDebugConsole console)
{
    private readonly List<string> _lines = [];

    public StringWriter Output { get; } = new();

    public TerminalConsoleSystemTestsBuilder WithTypedLine(string line)
    {
        _lines.Add(line);
        return this;
    }

    public TerminalConsoleSystem Build()
    {
        string typed = string.Join(Environment.NewLine, _lines);
        StringReader input = new(typed);
        return new TerminalConsoleSystem(console, input, Output);
    }
}
