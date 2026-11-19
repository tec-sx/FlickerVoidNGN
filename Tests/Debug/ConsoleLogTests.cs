namespace FlickerVoid.Engine.Debug.Tests;

public sealed class ConsoleLogTests
{
    private readonly ConsoleLogTestsBuilder _builder = new();

    [Fact]
    public void ShowsTheTypedLineThenWhatTheConsoleAnswered()
    {
        ConsoleLog log = _builder.Answering("two\nlines").Build();

        log.Submit("help");

        log.Lines.Should().Equal("> help", "two", "lines");
    }

    [Fact]
    public void IgnoresBlankLines()
    {
        ConsoleLog log = _builder.Build();

        log.Submit("   ");

        log.Lines.Should().BeEmpty();
    }

    [Fact]
    public void KeepsOnlyTheLatestLines()
    {
        ConsoleLog log = _builder.Answering("").Build();

        for (int index = 0; index < ConsoleLog.Capacity + 10; index++)
        {
            log.Submit($"line {index}");
        }

        log.Lines.Count.Should().Be(ConsoleLog.Capacity);
        log.Lines[^1].Should().Be($"> line {ConsoleLog.Capacity + 9}");
    }

    [Fact]
    public void RecallsEarlierLinesNewestFirst()
    {
        ConsoleLog log = _builder.Build();
        log.Submit("first");
        log.Submit("second");

        string? newest = log.Recall(1);
        string? older = log.Recall(2);
        string? none = log.Recall(3);

        newest.Should().Be("second");
        older.Should().Be("first");
        none.Should().BeNull();
    }
}
