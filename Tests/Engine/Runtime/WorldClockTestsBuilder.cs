using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Runtime.Tests;

/// <summary>Builds the world's clock under test over facts of its own, at a day, an hour and a speed.</summary>
internal sealed class WorldClockTestsBuilder
{
    private int _day;
    private double _hour;
    private float _speed;

    public IFactDatabase Facts { get; } = new FactDatabase(new FactSettings());

    public WorldClockTestsBuilder At(int day, double hour)
    {
        _day = day;
        _hour = hour;
        return this;
    }

    /// <summary>Game hours to a real minute.</summary>
    public WorldClockTestsBuilder WithSpeed(float speed)
    {
        _speed = speed;
        return this;
    }

    public WorldClock Build()
    {
        WorldClock clock = new(Facts) { Speed = _speed };
        if (_day != 0 || _hour != 0)
        {
            clock.Set(_day, _hour);
        }

        return clock;
    }
}
