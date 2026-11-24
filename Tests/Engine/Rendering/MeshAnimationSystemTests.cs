
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class MeshAnimationSystemTests : IDisposable
{
    private readonly MeshAnimationSystemTestsBuilder _builder = new();

    [Fact]
    public void AdvancesEachAnimatorByTheFrameTimeAtItsSpeed()
    {
        _builder.WithAnimator(time: 1, speed: 1).WithAnimator(time: 0, speed: 0.5f);

        MeshAnimationSystem system = _builder.Build();
        GameTime frame = MeshAnimationSystemTestsBuilder.Frame(seconds: 0.2f);

        system.Update(frame);

        _builder.Times.Should().Equal(1.2f, 0.1f);
    }

    [Fact]
    public void HoldsEveryAnimatorWhileTheGameIsPaused()
    {
        _builder.WithAnimator(time: 1, speed: 1);
        MeshAnimationSystem system = _builder.Build();
        GameTime frame = MeshAnimationSystemTestsBuilder.Frame(seconds: 0.2f) with { Paused = true };

        system.Update(frame);

        _builder.Times.Should().Equal(1f);
    }

    public void Dispose()
    {
        _builder.Dispose();
    }
}
