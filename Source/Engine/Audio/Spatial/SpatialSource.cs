using System.Numerics;

using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Audio.Spatial;

/// <summary>
/// Plays a sound from a direction: the inner source's left channel is taken as mono, run through Steam Audio's binaural effect for
/// <see cref="Direction"/> (head-related, so it is heard in front, behind, above) and scaled by <see cref="Gain"/>. The game thread
/// sets both as the listener and the sound move; the audio thread reads them a block at a time and never allocates.
/// </summary>
public sealed unsafe class SpatialSource : ISoundSource, IDisposable
{
    private readonly ISoundSource _inner;
    private readonly SteamAudio _audio;
    private readonly float[] _read = new float[SteamAudio.FrameSize * AudioFormat.Channels];
    private readonly float[] _mono = new float[SteamAudio.FrameSize];
    private readonly float[] _left = new float[SteamAudio.FrameSize];
    private readonly float[] _right = new float[SteamAudio.FrameSize];
    private readonly float[] _out = new float[SteamAudio.FrameSize * AudioFormat.Channels];
    private nint _effect;
    private int _available;
    private int _taken;
    private bool _ended;
    private float _x;
    private float _y;
    private float _z = -1;
    private float _gain = 1;

    public SpatialSource(ISoundSource inner, SteamAudio audio)
    {
        _inner = inner;
        _audio = audio;
        SteamAudioNative.AudioSettings settings = new() { SamplingRate = AudioFormat.SampleRate, FrameSize = SteamAudio.FrameSize };
        SteamAudioNative.BinauralEffectSettings effect = new() { Hrtf = audio.Hrtf };
        nint created = 0;
        if (SteamAudioNative.iplBinauralEffectCreate(audio.Context, &settings, &effect, &created) != 0)
        {
            throw new InvalidOperationException("Steam Audio could not create a binaural effect.");
        }

        _effect = created;
    }

    /// <summary>Where the sound is heard from, in the listener's axes (x right, y up, z behind).</summary>
    public Vector3 Direction
    {
        get => new(Volatile.Read(ref _x), Volatile.Read(ref _y), Volatile.Read(ref _z));
        set
        {
            Volatile.Write(ref _x, value.X);
            Volatile.Write(ref _y, value.Y);
            Volatile.Write(ref _z, value.Z);
        }
    }

    public float Gain
    {
        get => Volatile.Read(ref _gain);
        set => Volatile.Write(ref _gain, value);
    }

    public int Read(Span<float> stereo)
    {
        int frames = stereo.Length / AudioFormat.Channels;
        int written = 0;
        while (written < frames)
        {
            if (_taken >= _available)
            {
                if (_ended || !Fill())
                {
                    break;
                }
            }

            int count = Math.Min(frames - written, _available - _taken);
            _out.AsSpan(_taken * 2, count * 2).CopyTo(stereo.Slice(written * 2, count * 2));
            written += count;
            _taken += count;
        }

        return written;
    }

    public void Dispose()
    {
        if (_effect != 0)
        {
            nint effect = _effect;
            SteamAudioNative.iplBinauralEffectRelease(&effect);
            _effect = 0;
        }
    }

    private bool Fill()
    {
        int got = _inner.Read(_read);
        if (got <= 0)
        {
            _ended = true;
            return false;
        }

        if (got < SteamAudio.FrameSize)
        {
            _ended = true;
        }

        for (int frame = 0; frame < SteamAudio.FrameSize; frame++)
        {
            _mono[frame] = frame < got ? _read[frame * 2] : 0;
        }

        Apply();
        _available = got;
        _taken = 0;
        return true;
    }

    private void Apply()
    {
        Vector3 direction = Direction;
        float gain = Gain;
        SteamAudioNative.BinauralEffectParams parameters = new()
        {
            X = direction.X,
            Y = direction.Y,
            Z = direction.Z,
            Interpolation = SteamAudioNative.InterpolationBilinear,
            SpatialBlend = 1,
            Hrtf = _audio.Hrtf,
        };

        fixed (float* mono = _mono)
        fixed (float* left = _left)
        fixed (float* right = _right)
        {
            float* inputChannels = mono;
            float** inputData = &inputChannels;
            float** outputData = stackalloc float*[2];
            outputData[0] = left;
            outputData[1] = right;
            SteamAudioNative.AudioBuffer input = new() { Channels = 1, Samples = SteamAudio.FrameSize, Data = inputData };
            SteamAudioNative.AudioBuffer output = new() { Channels = 2, Samples = SteamAudio.FrameSize, Data = outputData };
            SteamAudioNative.iplBinauralEffectApply(_effect, &parameters, &input, &output);
        }

        for (int frame = 0; frame < SteamAudio.FrameSize; frame++)
        {
            _out[frame * 2] = _left[frame] * gain;
            _out[(frame * 2) + 1] = _right[frame] * gain;
        }
    }
}
