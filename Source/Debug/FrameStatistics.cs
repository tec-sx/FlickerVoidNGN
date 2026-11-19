namespace FlickerVoid.Engine.Debug;

/// <summary>Frame times over the last <see cref="HistoryLength"/> frames, for the frame rate, the slowest frame and a plot.</summary>
public sealed class FrameStatistics
{
    public const int HistoryLength = 120;

    private readonly float[] _milliseconds = new float[HistoryLength];
    private int _count;
    private int _next;

    /// <summary>The frame times in milliseconds, oldest first.</summary>
    public ReadOnlySpan<float> History
    {
        get
        {
            float[] ordered = new float[_count];
            int start = (_next - _count + HistoryLength) % HistoryLength;
            for (int index = 0; index < _count; index++)
            {
                ordered[index] = _milliseconds[(start + index) % HistoryLength];
            }

            return ordered;
        }
    }

    public float AverageMilliseconds { get; private set; }

    public float MaxMilliseconds { get; private set; }

    public float FramesPerSecond
    {
        get
        {
            if (AverageMilliseconds <= 0)
            {
                return 0;
            }

            return 1000 / AverageMilliseconds;
        }
    }

    public void Add(float seconds)
    {
        _milliseconds[_next] = seconds * 1000;
        _next = (_next + 1) % HistoryLength;
        _count = Math.Min(_count + 1, HistoryLength);

        float total = 0;
        float max = 0;
        for (int index = 0; index < _count; index++)
        {
            float value = _milliseconds[index];
            total += value;
            max = MathF.Max(max, value);
        }

        AverageMilliseconds = total / _count;
        MaxMilliseconds = max;
    }
}
