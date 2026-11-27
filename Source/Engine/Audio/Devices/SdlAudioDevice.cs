using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Audio.Mixing;

using Silk.NET.SDL;

namespace FlickerVoid.Engine.Audio.Devices;

/// <summary>Plays a mixer through SDL's default output device: SDL's audio thread pulls the mixer's blocks as it needs them.</summary>
public sealed unsafe class SdlAudioDevice : IDisposable
{
    private const ushort FloatSamples = 0x8120;

    private readonly Sdl _sdl;
    private readonly uint _device;
    private GCHandle _mixer;
    private bool _disposed;

    private SdlAudioDevice(Sdl sdl, uint device, GCHandle mixer)
    {
        _sdl = sdl;
        _device = device;
        _mixer = mixer;
    }

    /// <summary>
    /// Opens the default output device and starts playing the mixer; null, with the reason, where there is no device to open, as on a
    /// build server. <paramref name="driver"/> names SDL's audio driver in place of the system's, such as <c>dummy</c>, which plays to nowhere.
    /// </summary>
    public static SdlAudioDevice? TryOpen(Mixer mixer, out string problem, string? driver = null)
    {
        problem = string.Empty;
        Sdl sdl;
        try
        {
            sdl = Sdl.GetApi();
        }
        catch (Exception exception) when (exception is DllNotFoundException or FileNotFoundException or InvalidOperationException)
        {
            problem = exception.Message;
            return null;
        }

        if (driver is not null)
        {
            sdl.SetHint("SDL_AUDIODRIVER", driver);
        }

        if (sdl.InitSubSystem(Sdl.InitAudio) != 0)
        {
            problem = sdl.GetErrorS();
            return null;
        }

        GCHandle handle = GCHandle.Alloc(mixer);
        AudioSpec wanted = new()
        {
            Freq = AudioFormat.SampleRate,
            Format = FloatSamples,
            Channels = AudioFormat.Channels,
            Samples = AudioFormat.BlockFrames * 2,
            Callback = new PfnAudioCallback(&Fill),
            Userdata = (void*)GCHandle.ToIntPtr(handle),
        };
        AudioSpec got;
        uint device = sdl.OpenAudioDevice((byte*)null, 0, &wanted, &got, 0);
        if (device == 0)
        {
            problem = sdl.GetErrorS();
            handle.Free();
            sdl.QuitSubSystem(Sdl.InitAudio);
            return null;
        }

        sdl.PauseAudioDevice(device, 0);
        return new SdlAudioDevice(sdl, device, handle);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sdl.CloseAudioDevice(_device);
        _sdl.QuitSubSystem(Sdl.InitAudio);
        _mixer.Free();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Fill(void* userdata, byte* stream, int length)
    {
        Span<float> output = new(stream, length / sizeof(float));
        GCHandle handle = GCHandle.FromIntPtr((nint)userdata);
        if (handle.Target is Mixer mixer)
        {
            mixer.Render(output);
        }
        else
        {
            output.Clear();
        }
    }
}
