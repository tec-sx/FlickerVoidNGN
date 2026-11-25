using FlickerVoid.Engine.Runtime.Debugging;

namespace FlickerVoid.Engine.Runtime.Tests.Debugging;

public class DebugConsoleTests
{
    private readonly DebugConsoleTestsBuilder _builder = new();

    [Fact]
    public void RunsCommandsWithTheirWords()
    {
        Func<string[], string> run = Substitute.For<Func<string[], string>>();
        run(Arg.Any<string[]>()).Returns("Moved.");
        ConsoleCommand teleport = new("Player.Teleport", "Moves the player.", run);
        DebugConsole console = _builder.WithEntry(teleport).Build();

        string output = console.Execute("  player.teleport   1  2 3 ");

        output.Should().Be("Moved.");
        run.Received(1).Invoke(Arg.Is<string[]>(words => words.SequenceEqual(new[] { "1", "2", "3" })));
    }

    [Fact]
    public void ShowsAndSetsVariables()
    {
        ConsoleVariable<float> scale = ConsoleVariable.Create("Time.Debug.Scale", "Game speed.", 1f);
        DebugConsole console = _builder.WithEntry(scale).Build();

        string shown = console.Execute("time.debug.scale");
        string set = console.Execute("time.debug.scale 0.5");

        shown.Should().Be("Time.Debug.Scale = 1");
        set.Should().Be("Time.Debug.Scale = 0.5");
        scale.Value.Should().Be(0.5f);
    }

    [Fact]
    public void RaisesChangedWhenAVariableIsSet()
    {
        ConsoleVariable<bool> paused = ConsoleVariable.Create("Physics.Debug.Paused", "Stops physics.", false);
        Action<bool> changed = Substitute.For<Action<bool>>();
        paused.Changed += changed;
        DebugConsole console = _builder.WithEntry(paused).Build();

        console.Execute("physics.debug.paused true");

        changed.Received(1).Invoke(true);
    }

    [Fact]
    public void StringVariablesTakeTheRestOfTheLine()
    {
        ConsoleVariable<string> greeting = ConsoleVariable.Create("Debug.Greeting", "Text to show.", "hello");
        DebugConsole console = _builder.WithEntry(greeting).Build();

        console.Execute("debug.greeting good evening");

        greeting.Value.Should().Be("good evening");
    }

    [Fact]
    public void RejectsValuesOfTheWrongType()
    {
        ConsoleVariable<int> count = ConsoleVariable.Create("Debug.Count", "A number.", 3);
        DebugConsole console = _builder.WithEntry(count).Build();

        string output = console.Execute("debug.count three");

        output.Should().Be("'three' is not a valid Int32 for Debug.Count.");
        count.Value.Should().Be(3);
    }

    [Fact]
    public void ReportsUnknownNames()
    {
        DebugConsole console = _builder.Build();

        string output = console.Execute("fly");

        output.Should().Be("Unknown command 'fly'. Type help for a list.");
    }

    [Fact]
    public void IgnoresEmptyLines()
    {
        DebugConsole console = _builder.Build();

        string output = console.Execute("   ");

        output.Should().BeEmpty();
    }

    [Fact]
    public void HelpListsEntriesStartingWithTheGivenText()
    {
        ConsoleVariable<bool> paused = ConsoleVariable.Create("Physics.Debug.Paused", "Stops physics.", false);
        ConsoleVariable<float> scale = ConsoleVariable.Create("Time.Debug.Scale", "Game speed.", 1f);
        DebugConsole console = _builder.WithEntry(paused).WithEntry(scale).Build();

        string all = console.Execute("help");
        string physics = console.Execute("help physics");

        string[] lines = all.Split(Environment.NewLine);
        lines.Should().Equal(
            "help  Lists the commands and variables, or those starting with the given text.",
            "Physics.Debug.Paused  Stops physics.",
            "Time.Debug.Scale  Game speed.");
        physics.Should().Be("Physics.Debug.Paused  Stops physics.");
    }

    [Fact]
    public void RegisteringANameTwiceThrows()
    {
        ConsoleCommand first = new("Debug.Thing", "First.", static words => "");
        ConsoleCommand second = new("debug.thing", "Second.", static words => "");
        DebugConsole console = _builder.WithEntry(first).Build();

        Action add = () => console.Add(second);

        add.Should().Throw<ArgumentException>();
    }
}
