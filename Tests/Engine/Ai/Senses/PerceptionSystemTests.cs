using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Ai.Senses;

namespace FlickerVoid.Engine.Ai.Tests.Senses;

public sealed class PerceptionSystemTests : IDisposable
{
    private readonly PerceptionSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SeesATargetAheadWithinItsSightRadiusOnce()
    {
        _builder.Build();
        Entity fox = _builder.AddTarget(new Vector3(1, 0, 5));

        _builder.Run(0.5f);

        _builder.Seen.Should().ContainSingle().Which.Should().Be(new TargetSeenEvent(_builder.Guard, fox));
        PerceptionSystem perception = _builder.Game.Services.GetService<PerceptionSystem>()!;
        perception.GetSeen(_builder.Guard).Should().Equal(fox);
    }

    [Theory]
    [InlineData(0, 0, 9)]
    [InlineData(0, 0, -3)]
    [InlineData(5, 0, 1)]
    public void DoesNotSeeTargetsBeyondItsRadiusOrOutsideItsView(float x, float y, float z)
    {
        _builder.Build();
        _builder.AddTarget(new Vector3(x, y, z));

        _builder.Run(0.5f);

        _builder.Seen.Should().BeEmpty();
    }

    [Fact]
    public void DoesNotSeeThroughWalls()
    {
        _builder.Build();
        _builder.AddWall(3);
        _builder.AddTarget(new Vector3(0, 0, 5));

        _builder.Run(0.5f);

        _builder.Seen.Should().BeEmpty();
    }

    [Fact]
    public void SeesOutOfItsOwnBody()
    {
        _builder.WithBody().Build();
        Entity fox = _builder.AddTarget(new Vector3(0, 0, 5));

        _builder.Run(0.2f);

        _builder.Seen.Should().ContainSingle().Which.Target.Should().Be(fox);
    }

    [Fact]
    public void KeepsSeeingATargetUntilItPassesTheLoseSightRadius()
    {
        _builder.Build();
        Entity fox = _builder.AddTarget(new Vector3(0, 0, 5));
        _builder.Run(0.2f);

        _builder.MoveTo(fox, new Vector3(0, 0, 9));
        _builder.Run(0.2f);
        List<TargetLostEvent> lostAtNine = [.. _builder.Lost];
        _builder.MoveTo(fox, new Vector3(0, 0, 11));
        _builder.Run(0.2f);

        lostAtNine.Should().BeEmpty();
        _builder.Lost.Should().ContainSingle().Which.Should().Be(new TargetLostEvent(_builder.Guard, fox));
        _builder.Seen.Should().ContainSingle();
    }

    [Fact]
    public void LosesATargetThatIsGone()
    {
        _builder.Build();
        Entity fox = _builder.AddTarget(new Vector3(0, 0, 5));
        _builder.Run(0.2f);

        _builder.Remove(fox);
        _builder.Run(0.2f);

        _builder.Lost.Should().ContainSingle().Which.Target.Should().Be(fox);
    }

    [Fact]
    public void HearsNoisesWithinItsRangeTimesTheirLoudnessAndForgetsThemAfterItsMemory()
    {
        _builder.Build();
        Entity fox = _builder.AddTarget(new Vector3(0, 0, -8));
        Vector3 rattle = new(0, 0, -8);
        _builder.Game.Events.Send(new NoiseEvent(fox, rattle, 0.5f));
        _builder.Run(0.1f);
        List<NoiseHeardEvent> quiet = [.. _builder.Heard];

        _builder.Game.Events.Send(new NoiseEvent(fox, rattle));
        _builder.Run(1.5f);
        List<NoiseForgottenEvent> soon = [.. _builder.Forgotten];
        _builder.Run(1);

        quiet.Should().BeEmpty();
        _builder.Heard.Should().ContainSingle().Which.Should().Be(new NoiseHeardEvent(_builder.Guard, fox, rattle));
        soon.Should().BeEmpty();
        _builder.Forgotten.Should().ContainSingle().Which.Should().Be(new NoiseForgottenEvent(_builder.Guard, fox));
    }

    [Fact]
    public void DoesNotHearNoisesBeyondTheirRange()
    {
        _builder.Build();
        Entity fox = _builder.AddTarget(new Vector3(0, 0, -8));
        _builder.Game.Events.Send(new NoiseEvent(fox, new Vector3(0, 0, -8), 1, 5));

        _builder.Run(0.1f);

        _builder.Heard.Should().BeEmpty();
    }
}
