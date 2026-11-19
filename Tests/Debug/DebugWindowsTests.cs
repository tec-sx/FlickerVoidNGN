namespace FlickerVoid.Engine.Debug.Tests;

public sealed class DebugWindowsTests
{
    private readonly DebugWindowsTestsBuilder _builder = new();

    [Fact]
    public void GroupsWindowsByCategoryInTheOrderTheyWereAdded()
    {
        DebugWindows windows = _builder.WithWindow("Stats", "Engine").WithWindow("Lights", "Scene").WithWindow("Console", "Engine").Build();

        IReadOnlyList<DebugCategory> categories = windows.Categories;

        categories.Select(category => category.Name).Should().Equal("Engine", "Scene");
        categories[0].Windows.Select(window => window.Title).Should().Equal("Stats", "Console");
    }

    [Fact]
    public void RejectsTwoWindowsWithTheSameTitle()
    {
        DebugWindows windows = _builder.WithWindow("Stats", "Engine").Build();
        DebugWindow duplicate = DebugWindowsTestsBuilder.CreateWindow("stats", "Scene");

        Action add = () => windows.Add(duplicate);

        add.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SavesWhichWindowsAreOpenAndOpensThemAgain()
    {
        DebugWindows windows = _builder.WithWindow("Stats", "Engine", open: true).WithWindow("Lights", "Scene").Build();
        string json = windows.SaveLayout();

        DebugWindows fresh = new DebugWindowsTestsBuilder().WithWindow("Stats", "Engine").WithWindow("Lights", "Scene", open: true).Build();
        fresh.LoadLayout(json);

        fresh.Find("Stats")!.IsOpen.Should().BeTrue();
        fresh.Find("Lights")!.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void IgnoresSavedWindowsThatNoLongerExist()
    {
        DebugWindows windows = _builder.WithWindow("Stats", "Engine").Build();

        Action load = () => windows.LoadLayout("""{ "openWindows": ["Gone"] }""");

        load.Should().NotThrow();
        windows.Find("Stats")!.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void LeavesWindowsAloneWhenTheLayoutIsNotJson()
    {
        DebugWindows windows = _builder.WithWindow("Stats", "Engine", open: true).Build();

        windows.LoadLayout("not json");

        windows.Find("Stats")!.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void FindsWindowsByTitleIgnoringCase()
    {
        DebugWindows windows = _builder.WithWindow("Stats", "Engine").Build();

        windows.Find("STATS").Should().NotBeNull();
        windows.Find("Nope").Should().BeNull();
    }
}
