using FlickerVoid.Engine.Runtime.Debugging;

namespace FlickerVoid.Engine.Runtime.Tests.Debugging;

public class TerminalConsoleSystemTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly IDebugConsole _console = Substitute.For<IDebugConsole>();
    private readonly TerminalConsoleSystemTestsBuilder _builder;

    public TerminalConsoleSystemTests()
    {
        _builder = new TerminalConsoleSystemTestsBuilder(_console);
    }

    [Fact]
    public void RunsTypedLinesInFrameUpdatesAndPrintsWhatTheyShow()
    {
        _console.Execute("help").Returns("help  Lists the commands.");
        _console.Execute("quit").Returns("");
        using TerminalConsoleSystem terminal = _builder.WithTypedLine("help").WithTypedLine("quit").Build();
        terminal.Initialize();

        bool ranBoth = SpinWait.SpinUntil(
            () =>
            {
                terminal.Update(default);
                return _console.ReceivedCalls().Count() == 2;
            },
            Timeout);

        ranBoth.Should().BeTrue();
        Received.InOrder(() =>
        {
            _console.Execute("help");
            _console.Execute("quit");
        });
        string printed = _builder.Output.ToString();
        printed.Should().Be("help  Lists the commands." + Environment.NewLine);
    }
}
