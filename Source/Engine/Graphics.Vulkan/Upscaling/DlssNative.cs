using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace FlickerVoid.Engine.Graphics.Vulkan.Upscaling;

/// <summary>
/// The engine's DLSS library (Source/Native/Dlss, FlickerVoid.Dlss.h), which its build script puts where the engine's Vulkan project
/// copies it beside every program. Each call returns an NGX result.
/// </summary>
internal static unsafe partial class DlssNative
{
    public const string Library = "FlickerVoid.Dlss";

    /// <summary>Whether the library is beside the program.</summary>
    public static bool IsPresent()
    {
        return NativeLibrary.TryLoad(Library, typeof(DlssNative).Assembly, null, out _);
    }

    /// <summary>Whether an NGX result is a failure (0xBADxxxxx).</summary>
    public static bool Failed(uint result) => (result & 0xFFF00000) == 0xBAD00000;

    [LibraryImport(Library)]
    public static partial uint FvDlssInstanceExtensions(DlssApplication* application, [MarshalAs(UnmanagedType.Bool)] bool denoise, uint* count, ExtensionProperties** extensions);

    [LibraryImport(Library)]
    public static partial uint FvDlssDeviceExtensions(DlssApplication* application, [MarshalAs(UnmanagedType.Bool)] bool denoise, nint instance, nint physicalDevice, uint* count, ExtensionProperties** extensions);

    [LibraryImport(Library)]
    public static partial uint FvDlssInit(DlssApplication* application, nint instance, nint physicalDevice, nint device, nint getInstanceProcAddr, nint getDeviceProcAddr, nint* context);

    [LibraryImport(Library)]
    public static partial uint FvDlssCanDenoise(nint context);

    [LibraryImport(Library)]
    public static partial uint FvDlssShutdown(nint context);

    [LibraryImport(Library)]
    public static partial uint FvDlssRenderSize(nint context, DlssMode mode, [MarshalAs(UnmanagedType.Bool)] bool denoise, uint outputWidth, uint outputHeight, uint* renderWidth, uint* renderHeight);

    [LibraryImport(Library)]
    public static partial uint FvDlssCreate(nint context, nint commands, DlssMode mode, [MarshalAs(UnmanagedType.Bool)] bool denoise, uint renderWidth, uint renderHeight, uint outputWidth, uint outputHeight, nint* feature);

    [LibraryImport(Library)]
    public static partial uint FvDlssEvaluate(nint context, nint commands, nint feature, DlssFrame* frame);

    [LibraryImport(Library)]
    public static partial uint FvDlssRelease(nint feature);
}
