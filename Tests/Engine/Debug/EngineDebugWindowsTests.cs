using FlickerVoid.Engine.Graphics.Upscaling;
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
    public void AddsRenderingProfilerAndLightsToTheRenderingMenu()
    {
        _builder.WithRenderingWindows();

        _builder.RenderFrame(visible: true);

        DebugCategory rendering = _builder.Ui.Windows.Categories.Single(category => category.Name == "Rendering");
        rendering.Windows.Select(window => window.Title).Should().Equal("Rendering", "Profiler", "Lights");
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
        _builder.Renderer.RayTracing.Should().BeFalse("the window leaves ray tracing as the renderer has it");
        _builder.Renderer.DebugView.Should().Be(DebugView.Lit, "the window shows the lit frame until a view is picked");
        _builder.Renderer.Upscaling.Should().Be(UpscaleMode.Off, "the window leaves upscaling as the renderer has it");
    }

    [Fact]
    public void TheProfilerTurnsTheRenderersProfilingOnOnceShown()
    {
        _builder.WithRenderingWindows();
        _builder.RenderFrame(visible: false);

        _builder.Renderer!.Profiling.Should().BeFalse("nothing is timed until the profiler is shown");
        _builder.RenderFrame(visible: true);

        _builder.Renderer.Profiling.Should().BeTrue();
    }

    [Fact]
    public void TheProfilerDrawsTheRenderersPasses()
    {
        _builder.WithRenderingWindows();
        _builder.RenderFrame(visible: true);
        uint[] measuring = _builder.RenderFrame(visible: true);
        RenderProfile profile = _builder.Renderer!.Profile;
        for (int frame = 0; frame < RenderProfile.DefaultWindow; frame++)
        {
            profile.AddCpu("GeometryPass", 4);
            profile.AddCpu("LightingPass", 2);
            profile.EndFrame();
        }

        uint[] measured = _builder.RenderFrame(visible: true);

        profile.Passes.Should().HaveCount(2);
        int changed = measured.Where((pixel, index) => pixel != measuring[index]).Count();
        changed.Should().BeGreaterThan(0);
    }
}
