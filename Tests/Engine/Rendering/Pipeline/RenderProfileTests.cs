using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Tests.Pipeline;

public sealed class RenderProfileTests
{
    private readonly RenderProfileTestsBuilder _builder = new();

    [Fact]
    public void ShowsNothingBeforeItsFirstWindowIsOver()
    {
        RenderProfile profile = _builder.WithWindow(2).Build();

        profile.AddCpu("GeometryPass", 1);
        profile.EndFrame();

        profile.Passes.Should().BeEmpty();
        profile.CpuMilliseconds.Should().Be(0);
    }

    [Fact]
    public void AveragesEachPassOverItsWindow()
    {
        RenderProfile profile = _builder.WithWindow(2).Build();

        profile.AddCpu("GeometryPass", 1);
        profile.EndFrame();
        profile.AddCpu("GeometryPass", 3);
        profile.EndFrame();

        profile.Passes.Should().ContainSingle();
        profile.Passes[0].Name.Should().Be("GeometryPass");
        profile.Passes[0].CpuMilliseconds.Should().BeApproximately(2, 1e-9);
    }

    [Fact]
    public void AddsTheTimesAPassTakesInOneFrame()
    {
        RenderProfile profile = _builder.WithWindow(1).Build();

        profile.AddCpu("GeometryPass", 1);
        profile.AddCpu("GeometryPass", 0.5);
        profile.EndFrame();

        profile.Passes[0].CpuMilliseconds.Should().BeApproximately(1.5, 1e-9);
    }

    [Fact]
    public void KeepsTheLastAveragesUntilTheNextWindowIsOver()
    {
        RenderProfile profile = _builder.WithWindow(2).Build();
        profile.AddCpu("GeometryPass", 2);
        profile.EndFrame();
        profile.AddCpu("GeometryPass", 2);
        profile.EndFrame();

        profile.AddCpu("GeometryPass", 10);
        profile.EndFrame();

        profile.Passes[0].CpuMilliseconds.Should().BeApproximately(2, 1e-9);
    }

    [Fact]
    public void MatchesGpuTimingsToPassesByName()
    {
        RenderProfile profile = _builder.WithWindow(1).Build();

        profile.AddGpu([new GpuTiming("ShadowPass", 0, 0, 0.75), new GpuTiming("GeometryPass", 0, 0.75, 2)]);
        profile.AddCpu("ShadowPass", 0.25);
        profile.AddCpu("GeometryPass", 0.5);
        profile.EndFrame();

        profile.Passes.Should().Equal(new PassTiming("ShadowPass", 0, 0.25, 0.75), new PassTiming("GeometryPass", 0, 0.5, 2));
    }

    [Fact]
    public void ListsPassesInTheOrderTheyFirstRan()
    {
        RenderProfile profile = _builder.WithWindow(1).Build();

        profile.AddCpu("Extract", 0.1);
        profile.AddGpu([new GpuTiming("ShadowPass", 0, 0, 1), new GpuTiming("Overlay", 0, 1, 0.5)]);
        profile.AddCpu("ShadowPass", 0.2);
        profile.EndFrame();

        profile.Passes.Select(pass => pass.Name).Should().Equal("Extract", "ShadowPass", "Overlay");
    }

    [Fact]
    public void KeepsTheDepthOfNestedGpuTimers()
    {
        RenderProfile profile = _builder.WithWindow(1).Build();

        profile.AddGpu([new GpuTiming("ShadowPass", 0, 0, 1), new GpuTiming("Sun", 1, 0, 0.5)]);
        profile.EndFrame();

        profile.Passes.Select(pass => pass.Depth).Should().Equal(0, 1);
    }

    [Fact]
    public void TotalsTheFrameWithoutCountingNestedTimersTwice()
    {
        RenderProfile profile = _builder.WithWindow(1).Build();

        profile.AddGpu([new GpuTiming("ShadowPass", 0, 0, 1), new GpuTiming("Sun", 1, 0, 0.5), new GpuTiming("Overlay", 0, 1, 0.25)]);
        profile.AddCpu("Extract", 0.5);
        profile.AddCpu("ShadowPass", 0.25);
        profile.EndFrame();

        profile.GpuMilliseconds.Should().BeApproximately(1.25, 1e-9);
        profile.CpuMilliseconds.Should().BeApproximately(0.75, 1e-9);
    }

    [Fact]
    public void AveragesGpuTimesOverTheFramesThatHadThem()
    {
        RenderProfile profile = _builder.WithWindow(2).Build();

        profile.AddCpu("ShadowPass", 1);
        profile.EndFrame();
        profile.AddGpu([new GpuTiming("ShadowPass", 0, 0, 4)]);
        profile.AddCpu("ShadowPass", 1);
        profile.EndFrame();

        profile.Passes[0].GpuMilliseconds.Should().BeApproximately(4, 1e-9);
    }
}
