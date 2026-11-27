namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>Anything a voice plays: a decoded clip now, streams and instruments later. Read on the audio thread, so it must not allocate or lock.</summary>
public interface ISoundSource
{
    /// <summary>Fills <paramref name="stereo"/> with interleaved frames at the mixer's rate and returns how many it wrote; fewer than asked means it ended.</summary>
    int Read(Span<float> stereo);
}
