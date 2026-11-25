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
}
