using System.Numerics;

using FlickerVoid.Engine.Audio.Mixing;

namespace FlickerVoid.Engine.Audio.Spatial;

/// <summary>
/// Steam Audio's context and default head-related transfer function, the one place the engine's 3D sound comes from when the
/// <c>phonon</c> library is there; <see cref="TryCreate"/> gives null where it is not, and the sounds are panned as in step 1.
/// </summary>
public sealed unsafe class SteamAudio : IDisposable
{
    /// <summary>The frames Steam Audio processes at a time; the mixer's sources read in blocks of this many.</summary>
    public const int FrameSize = 256;

    private nint _context;
    private nint _hrtf;

    private SteamAudio(nint context, nint hrtf)
    {
        _context = context;
        _hrtf = hrtf;
    }

    /// <summary>Loads the library and its default HRTF; null when the library can't be loaded or created.</summary>
    public static SteamAudio? TryCreate()
    {
        SteamAudioNative.Register();
        try
        {
            SteamAudioNative.ContextSettings settings = new() { Version = SteamAudioNative.Version, SimdLevel = SteamAudioNative.SimdAvx2 };
            nint context = 0;
            if (SteamAudioNative.iplContextCreate(&settings, &context) != 0)
            {
                return null;
            }

            SteamAudioNative.AudioSettings audio = new() { SamplingRate = AudioFormat.SampleRate, FrameSize = FrameSize };
            SteamAudioNative.HrtfSettings hrtfSettings = new() { Type = 0, Volume = 1 };
            nint hrtf = 0;
            if (SteamAudioNative.iplHRTFCreate(context, &audio, &hrtfSettings, &hrtf) != 0)
            {
                SteamAudioNative.iplContextRelease(&context);
                return null;
            }

            return new SteamAudio(context, hrtf);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return null;
        }
    }

    internal nint Context => _context;

    internal nint Hrtf => _hrtf;

    /// <summary>The direction of a point seen from the ears, in Steam Audio's axes: x to the right, y up, z behind.</summary>
    public static Vector3 ToListenerSpace(Vector3 toSource, Vector3 right, Vector3 up, Vector3 forward)
    {
        if (toSource.LengthSquared() < 1e-8f)
        {
            return new Vector3(0, 0, -1);
        }

        Vector3 direction = Vector3.Normalize(toSource);
        return new Vector3(Vector3.Dot(direction, right), Vector3.Dot(direction, up), -Vector3.Dot(direction, forward));
    }

    public void Dispose()
    {
        if (_hrtf != 0)
        {
            nint hrtf = _hrtf;
            SteamAudioNative.iplHRTFRelease(&hrtf);
            _hrtf = 0;
        }

        if (_context != 0)
        {
            nint context = _context;
            SteamAudioNative.iplContextRelease(&context);
            _context = 0;
        }
    }
}
