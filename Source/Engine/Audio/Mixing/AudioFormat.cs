namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>The one format the mixer works in: interleaved stereo floats at 48 kHz, in blocks of 256 frames.</summary>
public static class AudioFormat
{
    public const int SampleRate = 48000;

    public const int Channels = 2;

    /// <summary>The most frames mixed at once; commands take hold between blocks, so within 5.3 ms.</summary>
    public const int BlockFrames = 256;
}
