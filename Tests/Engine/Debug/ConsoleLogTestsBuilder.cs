using FlickerVoid.Engine.Runtime.Debugging;

using NSubstitute;

namespace FlickerVoid.Engine.Debug.Tests;

/// <summary>Builds a console log over a console that answers every line the same way.</summary>
internal sealed class ConsoleLogTestsBuilder
{
    private readonly IDebugConsole _console = Substitute.For<IDebugConsole>();
    private string _answer = "";

    public ConsoleLogTestsBuilder Answering(string answer)
    {
        _answer = answer;
        return this;
    }

    public ConsoleLog Build()
    {
        _console.Execute(Arg.Any<string>()).Returns(_answer);
        return new ConsoleLog(_console);
    }
}
