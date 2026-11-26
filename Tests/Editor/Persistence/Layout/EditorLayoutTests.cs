using FlickerVoid.Editor.Persistence.Layout;

namespace FlickerVoid.Editor.Persistence.Tests.Layout;

public sealed class EditorLayoutTests : IDisposable
{
    private readonly EditorLayoutTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void StartsWithTheContextPanelOverTheOutliner()
    {
        EditorLayout layout = _builder.Build();

        layout.PanelsSwapped.Should().BeFalse();
    }

    [Fact]
    public void RemembersSwappedPanelsForTheNextRun()
    {
        EditorLayout layout = _builder.Build();

        layout.PanelsSwapped = true;

        _builder.Build().PanelsSwapped.Should().BeTrue();
    }

    [Fact]
    public void StartsOverWhenTheFileCantBeRead()
    {
        EditorLayout layout = _builder.WithBrokenFile().Build();

        layout.PanelsSwapped.Should().BeFalse();
    }

    [Fact]
    public void StartsWithTheDrawerClosedAndThePanelsAtTheirDesignedSizes()
    {
        EditorLayout layout = _builder.Build();

        layout.ContentOpen.Should().BeFalse();
        layout.ContentHeight.Should().Be(318);
        layout.DayDockHeight.Should().Be(300);
        layout.SideWidth.Should().Be(320);
    }

    [Fact]
    public void RemembersTheDrawerAndThePanelSizesForTheNextRun()
    {
        EditorLayout layout = _builder.Build();

        layout.ContentOpen = true;
        layout.ContentHeight = 420;
        layout.DayDockHeight = 260;
        layout.SideWidth = 380;

        EditorLayout next = _builder.Build();
        next.ContentOpen.Should().BeTrue();
        next.ContentHeight.Should().Be(420);
        next.DayDockHeight.Should().Be(260);
        next.SideWidth.Should().Be(380);
    }

    [Fact]
    public void NeverBringsAPanelBackTooSmallToGrab()
    {
        EditorLayout layout = _builder.Build();

        layout.ContentHeight = 4;
        layout.SideWidth = 0;

        EditorLayout next = _builder.Build();
        next.ContentHeight.Should().Be(EditorLayout.MinimumSize);
        next.SideWidth.Should().Be(EditorLayout.MinimumSize);
    }
}
