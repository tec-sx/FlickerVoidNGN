namespace FlickerVoid.Engine.Runtime.Tests;

public class GameLoopTests
{
    private static readonly TimeSpan Step = GameLoop.DefaultFixedStep;

    private readonly List<GameTime> _steps = [];
    private readonly List<GameTime> _frames = [];
    private readonly GameLoop _loop;

    public GameLoopTests()
    {
        _loop = new GameLoopTestsBuilder().OnFixedUpdate(_steps.Add).OnUpdate(_frames.Add).Build();
    }

    [Fact]
    public void RunsOneStepPerFrameAtTheFixedRate()
    {
        _loop.Advance(Step);
        _loop.Advance(Step);

        GameTime expected = new(Step, Step * 2, 2);
        _steps.Should().HaveCount(2);
        _steps[1].Should().Be(expected);
        _frames[1].Should().Be(expected);
    }

    [Fact]
    public void CatchesUpWithSeveralStepsAfterALongFrame()
    {
        _loop.Advance(Step * 3);

        _steps.Should().HaveCount(3);
        _frames.Should().ContainSingle();
    }

    [Fact]
    public void WaitsForAFullStepOnFastFrames()
    {
        TimeSpan half = Step / 2;

        _loop.Advance(half);
        int stepsAfterFirst = _steps.Count;
        float interpolation = _loop.Interpolation;
        _loop.Advance(half);

        stepsAfterFirst.Should().Be(0);
        interpolation.Should().BeApproximately(0.5f, 0.001f);
        _steps.Should().ContainSingle();
        _frames.Should().HaveCount(2);
    }

    [Fact]
    public void CountsAFrameCloseToAStepAsExactlyOneStep()
    {
        TimeSpan slightlyShort = Step - TimeSpan.FromTicks(Step.Ticks / 100);

        for (int frame = 0; frame < 100; frame++)
        {
            _loop.Advance(slightlyShort);
        }

        _steps.Should().HaveCount(100);
        _loop.Interpolation.Should().Be(0f);
    }

    [Fact]
    public void ClampsLongFramesToMaxElapsed()
    {
        _loop.Advance(TimeSpan.FromSeconds(10));

        int expectedSteps = (int)(GameLoop.DefaultMaxElapsed.Ticks / Step.Ticks);
        _steps.Should().HaveCount(expectedSteps);
        _frames[0].Elapsed.Should().Be(GameLoop.DefaultMaxElapsed);
    }

    [Fact]
    public void IgnoresNegativeTime()
    {
        _loop.Advance(TimeSpan.FromSeconds(-1));

        _steps.Should().BeEmpty();
        _frames[0].Elapsed.Should().Be(TimeSpan.Zero);
    }
}
