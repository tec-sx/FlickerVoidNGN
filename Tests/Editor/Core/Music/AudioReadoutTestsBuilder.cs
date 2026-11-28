using FlickerVoid.Editor.Core.Music;
using FlickerVoid.Engine.Audio.Devices;

namespace FlickerVoid.Editor.Core.Tests.Music;

/// <summary>Builds the music editor's audio readout, with reports of a device that kept up unless told otherwise.</summary>
internal sealed class AudioReadoutTestsBuilder
{
    public AudioReadout Build(int bufferFrames = 1024) => new(bufferFrames);

    /// <summary>A second of a device's report: 47 calls, the mixer's load and the longest wait in milliseconds.</summary>
    public static AudioTimingReport Report(double load = 0.004, long late = 0, double longestGap = 21)
    {
        TimeSpan gap = TimeSpan.FromMilliseconds(longestGap);
        TimeSpan render = TimeSpan.FromMilliseconds(0.1);
        return new AudioTimingReport(47, late, gap, render, load);
    }
}
