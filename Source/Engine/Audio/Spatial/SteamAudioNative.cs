using System.Reflection;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Audio.Spatial;

/// <summary>The few calls of Valve's Steam Audio (<c>phonon</c>, Apache 2.0) the engine uses: a context, the default HRTF and binaural effects.</summary>
internal static unsafe partial class SteamAudioNative
{
    public const string Library = "phonon";

    /// <summary>The API version this binding was written against (4.6.1).</summary>
    public const uint Version = (4u << 16) | (6u << 8) | 1u;

    private static readonly object Gate = new();
    private static bool _registered;

    [StructLayout(LayoutKind.Sequential)]
    public struct ContextSettings
    {
        public uint Version;
        public nint LogCallback;
        public nint AllocateCallback;
        public nint FreeCallback;
        public int SimdLevel;
        public int Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AudioSettings
    {
        public int SamplingRate;
        public int FrameSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HrtfSettings
    {
        public int Type;
        public nint SofaFileName;
        public nint SofaData;
        public int SofaDataSize;
        public float Volume;
        public int NormType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BinauralEffectSettings
    {
        public nint Hrtf;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BinauralEffectParams
    {
        public float X;
        public float Y;
        public float Z;
        public int Interpolation;
        public float SpatialBlend;
        public nint Hrtf;
        public nint PeakDelays;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AudioBuffer
    {
        public int Channels;
        public int Samples;
        public float** Data;
    }

    public const int SimdAvx2 = 3;

    public const int InterpolationBilinear = 1;

    [LibraryImport(Library)]
    public static partial int iplContextCreate(ContextSettings* settings, nint* context);

    [LibraryImport(Library)]
    public static partial void iplContextRelease(nint* context);

    [LibraryImport(Library)]
    public static partial int iplHRTFCreate(nint context, AudioSettings* audio, HrtfSettings* settings, nint* hrtf);

    [LibraryImport(Library)]
    public static partial void iplHRTFRelease(nint* hrtf);

    [LibraryImport(Library)]
    public static partial int iplBinauralEffectCreate(nint context, AudioSettings* audio, BinauralEffectSettings* settings, nint* effect);

    [LibraryImport(Library)]
    public static partial int iplBinauralEffectApply(nint effect, BinauralEffectParams* parameters, AudioBuffer* input, AudioBuffer* output);

    [LibraryImport(Library)]
    public static partial void iplBinauralEffectRelease(nint* effect);

    /// <summary>Lets the <c>phonon</c> imports find the library beside the program or in the engine's <c>artifacts/native</c> folder.</summary>
    public static void Register()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            _registered = true;
            NativeLibrary.SetDllImportResolver(typeof(SteamAudioNative).Assembly, Resolve);
        }
    }

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Library)
        {
            return 0;
        }

        string file = OperatingSystem.IsWindows() ? "phonon.dll" : "libphonon.so";
        string rid = OperatingSystem.IsWindows() ? "win-x64" : "linux-x64";
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        string beside = Path.Combine(AppContext.BaseDirectory, file);
        if (File.Exists(beside) && NativeLibrary.TryLoad(beside, out nint handle))
        {
            return handle;
        }

        while (folder is not null)
        {
            string path = Path.Combine(folder.FullName, "artifacts", "native", rid, file);
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out handle))
            {
                return handle;
            }

            folder = folder.Parent;
        }

        return 0;
    }
}
