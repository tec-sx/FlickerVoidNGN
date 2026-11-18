using FlickerVoid.Engine.Runtime;

namespace FlickerVoid.Engine.Runtime.Tests;

public class GameLoopTests
{
    private static readonly TimeSpan Step = GameLoop.DefaultFixedStep;

    private readonly GameLoop _loop = new();
    private readonly List<GameTime> _steps = [];
    private readonly List<GameTime> _frames = [];

    public GameLoopTests()
    {
        _loop.FixedUpdate += _steps.Add;
        _loop.Update += _frames.Add;
    }

    [Fact]
    public void RunsOneStepPerFrameAtTheFixedRate()
    {
        _loop.Advance(Step);
        _loop.Advance(Step);

        Assert.Equal(2, _steps.Count);
        Assert.Equal(new GameTime(Step, Step * 2, 2), _steps[1]);
        Assert.Equal(new GameTime(Step, Step * 2, 2), _frames[1]);
    }

    [Fact]
    public void CatchesUpWithSeveralStepsAfterALongFrame()
    {
        _loop.Advance(Step * 3);

        Assert.Equal(3, _steps.Count);
        Assert.Single(_frames);
    }

    [Fact]
    public void WaitsForAFullStepOnFastFrames()
    {
        TimeSpan half = Step / 2;

        _loop.Advance(half);
        int stepsAfterFirst = _steps.Count;
        float interpolation = _loop.Interpolation;
        _loop.Advance(half);

        Assert.Equal(0, stepsAfterFirst);
        Assert.Equal(0.5f, interpolation, 0.001f);
        Assert.Single(_steps);
        Assert.Equal(2, _frames.Count);
    }

    [Fact]
    public void CountsAFrameCloseToAStepAsExactlyOneStep()
    {
        TimeSpan slightlyShort = Step - TimeSpan.FromTicks(Step.Ticks / 100);

        for (int frame = 0; frame < 100; frame++)
        {
            _loop.Advance(slightlyShort);
        }

        Assert.Equal(100, _steps.Count);
        Assert.Equal(0f, _loop.Interpolation);
    }

    [Fact]
    public void ClampsLongFramesToMaxElapsed()
    {
        _loop.Advance(TimeSpan.FromSeconds(10));

        int expectedSteps = (int)(GameLoop.DefaultMaxElapsed.Ticks / Step.Ticks);
        Assert.Equal(expectedSteps, _steps.Count);
        Assert.Equal(GameLoop.DefaultMaxElapsed, _frames[0].Elapsed);
    }

    [Fact]
    public void IgnoresNegativeTime()
    {
        _loop.Advance(TimeSpan.FromSeconds(-1));

        Assert.Empty(_steps);
        Assert.Equal(TimeSpan.Zero, _frames[0].Elapsed);
    }
}
