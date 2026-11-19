namespace FlickerVoid.Engine.Debug.Tests;

/// <summary>Builds frame statistics that have seen the given frame times, in seconds.</summary>
internal sealed class FrameStatisticsTestsBuilder
{
    private readonly List<float> _frames = [];

    public FrameStatisticsTestsBuilder WithFrames(params float[] seconds)
    {
        _frames.AddRange(seconds);
        return this;
    }

    public FrameStatisticsTestsBuilder WithRepeatedFrames(int count, float seconds)
    {
        for (int index = 0; index < count; index++)
        {
            _frames.Add(seconds);
        }

        return this;
    }

    public FrameStatistics Build()
    {
        FrameStatistics statistics = new();
        foreach (float seconds in _frames)
        {
            statistics.Add(seconds);
        }

        return statistics;
    }
}
