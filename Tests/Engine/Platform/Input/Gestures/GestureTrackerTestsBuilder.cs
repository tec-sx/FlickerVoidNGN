using FlickerVoid.Engine.Platform.Input.Gestures;

namespace FlickerVoid.Engine.Platform.Tests.Input.Gestures;

/// <summary>Builds a gesture tracker, optionally losing mash presses over time.</summary>
internal sealed class GestureTrackerTestsBuilder
{
    private float _decay;

    public GestureTrackerTestsBuilder WithMashDecay(float pressesPerSecond)
    {
        _decay = pressesPerSecond;
        return this;
    }

    public GestureTracker Build() => new() { MashDecayPerSecond = _decay };
}
