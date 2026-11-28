namespace FlickerVoid.Engine.Audio.Devices;

/// <summary>
/// How a device's audio thread kept up since the last report: the times it asked for sound, how many of those came so late that
/// what it had queued may have run out (heard as a crackle), the longest wait between two, the longest the mixer took to fill
/// one, and the share of the sound's own time spent mixing it.
/// </summary>
public readonly record struct AudioTimingReport(long Callbacks, long Late, TimeSpan LongestGap, TimeSpan LongestRender, double Load);
