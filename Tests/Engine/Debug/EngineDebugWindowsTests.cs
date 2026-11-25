using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Debug.Tests;

public sealed class EngineDebugWindowsTests : IDisposable
{
    private readonly DebugUiTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AddsStatsAndConsoleToTheEngineMenu()
    {
        _builder.WithEngineWindows();

        _builder.RenderFrame(visible: true);

        DebugCategory engine = _builder.Ui.Windows.Categories.Single(category => category.Name == "Engine");
        engine.Windows.Select(window => window.Title).Should().Equal("Stats", "Console");
    }

    [Fact]
    public void AddsRenderingAndLightsToTheRenderingMenu()
    {
        _builder.WithRenderingWindows();

        _builder.RenderFrame(visible: true);

        DebugCategory rendering = _builder.Ui.Windows.Categories.Single(category => category.Name == "Rendering");
        rendering.Windows.Select(window => window.Title).Should().Equal("Rendering", "Lights");
    }

    [Fact]
    public void DrawsEveryEngineWindowWhenOpen()
    {
        _builder.WithEngineWindows().WithRenderingWindows();
        uint[] closed = new DebugUiTestsBuilder().WithEngineWindows().WithRenderingWindows().RenderAndDispose(visible: false);

        _builder.RenderFrame(visible: true);
        uint[] opened = _builder.RenderFrame(visible: true);

        int changed = opened.Where((pixel, index) => pixel != closed[index]).Count();
        changed.Should().BeGreaterThan(5000);
    }

    [Fact]
    public void SwitchingTheRenderingFeaturesIsTheRenderersOwnSetting()
    {
        _builder.WithRenderingWindows();
        _builder.RenderFrame(visible: true);

        _builder.Renderer!.LocalLights.Should().BeTrue("the window starts with the renderer's own settings");
        _builder.Renderer.Probes.Should().BeTrue();
        _builder.Renderer.DebugView.Should().Be(DebugView.Lit, "the window shows the lit frame until a view is picked");
    }
}
