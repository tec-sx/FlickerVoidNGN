using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Settings;

namespace FlickerVoid.Engine.Rendering.Tests.Pipeline;

public sealed class RenderingSettingsTests : IDisposable
{
    private readonly RenderingSettingsTestsBuilder _builder = new();

    [Fact]
    public void RenderingAddsItsVariablesToTheConsole()
    {
        RenderingSettings settings = _builder.BuildSettings();

        string shown = _builder.Console.Execute("r.raytracing");
        string set = _builder.Console.Execute("r.upscaling dlaa");

        shown.Should().Be("r.RayTracing = true");
        set.Should().Be("r.Upscaling = Dlaa");
        settings.Upscaling.Value.Should().Be(UpscaleMode.Dlaa);
        settings.ViewportUpscaling.Value.Should().Be(UpscaleMode.Off);
    }

    [Fact]
    public void TheDebugViewIsNotSaved()
    {
        RenderingSettings settings = _builder.BuildSettings();
        settings.DebugView.Value = DebugView.Normal;
        settings.RayTracing.Value = false;

        string json = SettingsFile.Write(_builder.Console, "settings.schema.json");

        json.Should().Contain("\"r.RayTracing\": false").And.NotContain("r.DebugView");
    }

    [Fact]
    public void RaisesChangedWhenAnyVariableChanges()
    {
        RenderingSettings settings = _builder.BuildSettings();
        int changes = 0;
        settings.Changed += () => changes++;

        _builder.Console.Execute("r.probes off");
        _builder.Console.Execute("r.debugview albedo");

        changes.Should().Be(2);
    }

    [Fact]
    public void AppliesTheGameWindowsSwitchesToARenderer()
    {
        RenderingSettings settings = _builder.BuildSettings();
        SceneRenderer renderer = _builder.BuildRenderer();
        settings.LocalLights.Value = false;
        settings.Probes.Value = false;
        settings.RayTracing.Value = false;
        settings.LiveProbes.Value = false;
        settings.Upscaling.Value = UpscaleMode.Dlaa;
        settings.ViewportUpscaling.Value = UpscaleMode.Quality;
        settings.RayReconstruction.Value = false;
        settings.DepthFades.Value = false;
        settings.DebugView.Value = DebugView.Depth;

        settings.Apply(renderer);

        renderer.LocalLights.Should().BeFalse();
        renderer.Probes.Should().BeFalse();
        renderer.RayTracing.Should().BeFalse();
        renderer.LiveProbes.Should().BeFalse();
        renderer.Upscaling.Should().Be(UpscaleMode.Dlaa);
        renderer.RayReconstruction.Should().BeFalse();
        renderer.DepthFades.Should().BeFalse();
        renderer.DebugView.Should().Be(DebugView.Depth);
    }

    [Fact]
    public void TheViewportTakesItsOwnUpscaling()
    {
        RenderingSettings settings = _builder.BuildSettings();
        SceneRenderer renderer = _builder.BuildRenderer();
        settings.Upscaling.Value = UpscaleMode.Dlaa;

        settings.ApplyToViewport(renderer);

        renderer.Upscaling.Should().Be(UpscaleMode.Off);
        renderer.RayTracing.Should().BeTrue();
    }

    [Fact]
    public void CapturesARenderersSwitches()
    {
        SceneRenderer renderer = _builder.BuildRenderer();
        renderer.RayTracing = false;
        renderer.Upscaling = UpscaleMode.Off;
        renderer.DebugView = DebugView.Albedo;

        RenderingSettings settings = RenderingSettings.Capture(renderer);

        settings.RayTracing.Value.Should().BeFalse();
        settings.Upscaling.Value.Should().Be(UpscaleMode.Off);
        settings.DebugView.Value.Should().Be(DebugView.Albedo);
        settings.LocalLights.Value.Should().BeTrue();
    }

    public void Dispose()
    {
        _builder.Dispose();
    }
}
