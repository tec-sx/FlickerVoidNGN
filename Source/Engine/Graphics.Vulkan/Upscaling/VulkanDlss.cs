using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Silk.NET.Vulkan;

using FlickerVoid.Engine.Graphics.Upscaling;

namespace FlickerVoid.Engine.Graphics.Vulkan.Upscaling;

/// <summary>
/// DLSS on one Vulkan device through the engine's DLSS library: the instance and device extensions NGX needs before they are made,
/// then NGX on the device, the size each mode draws at, and DLSS features made and evaluated in command buffers.
/// </summary>
internal sealed unsafe class VulkanDlss : IDisposable
{
    /// <summary>NGX's project id for the engine, a GUID as NGX asks of engines of their own.</summary>
    private const string ProjectId = "dc29c3ae-9728-46c0-b306-f3190660e8f9";

    /// <summary>Promoted to Vulkan 1.2, whose buffer device address the device turns on instead; Vulkan forbids both.</summary>
    private const string BufferDeviceAddressExtension = "VK_EXT_buffer_device_address";

    private readonly ILogger _logger;
    private readonly nint[] _strings;
    private readonly DlssApplication _application;
    private nint _context;
    private bool _disposed;

    private VulkanDlss(ILogger logger)
    {
        _logger = logger;
        Version? version = typeof(VulkanDlss).Assembly.GetName().Version;
        string engineVersion = version?.ToString(3) ?? "0.0.0";
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (localData.Length == 0)
        {
            localData = Path.GetTempPath();
        }

        DataPath = Path.Combine(localData, "FlickerVoid", "Dlss");
        Directory.CreateDirectory(DataPath);
        _strings =
        [
            Marshal.StringToCoTaskMemUTF8(ProjectId),
            Marshal.StringToCoTaskMemUTF8(engineVersion),
            Marshal.StringToCoTaskMemUTF8(DataPath),
            Marshal.StringToCoTaskMemUTF8(AppContext.BaseDirectory),
        ];
        _application = new DlssApplication
        {
            ProjectId = (byte*)_strings[0],
            EngineVersion = (byte*)_strings[1],
            DataPath = (byte*)_strings[2],
            RuntimePath = (byte*)_strings[3],
        };
    }

    /// <summary>The instance extensions NGX needs.</summary>
    public string[] InstanceExtensions { get; private set; } = [];

    /// <summary>The folder NGX writes its log to.</summary>
    public string DataPath { get; }

    /// <summary>
    /// DLSS where the engine's DLSS library is beside the program and NGX names the instance extensions it needs; otherwise logs
    /// why not and returns null.
    /// </summary>
    public static VulkanDlss? TryLoad(Vk vk, ILogger logger)
    {
        if (!DlssNative.IsPresent())
        {
            logger.LogInformation("DLSS is off: the engine's DLSS library ({Library}) is not beside the program; Source/Native/Dlss builds it.", DlssNative.Library);
            return null;
        }

        VulkanDlss dlss = new(logger);
        DlssApplication application = dlss._application;
        uint count = 0;
        ExtensionProperties* extensions = null;
        uint result = DlssNative.FvDlssInstanceExtensions(&application, &count, &extensions);
        if (DlssNative.Failed(result))
        {
            dlss.LogOff($"NGX could not name the instance extensions it needs (NGX result 0x{result:X8})");
            dlss.Dispose();
            return null;
        }

        string[] names = GetNames(extensions, count);
        string? missing = Array.Find(names, name => !vk.IsInstanceExtensionPresent(name));
        if (missing is not null)
        {
            dlss.LogOff($"NGX needs the instance extension {missing}, which Vulkan lacks");
            dlss.Dispose();
            return null;
        }

        logger.LogDebug("NGX needs the instance extensions {Extensions}.", names);
        dlss.InstanceExtensions = names;
        return dlss;
    }

    /// <summary>The device extensions NGX needs on the physical device, or null, having logged why, where DLSS doesn't run there.</summary>
    public string[]? GetDeviceExtensions(Vk vk, Instance instance, PhysicalDevice physicalDevice)
    {
        DlssApplication application = _application;
        uint count = 0;
        ExtensionProperties* extensions = null;
        uint result = DlssNative.FvDlssDeviceExtensions(&application, instance.Handle, physicalDevice.Handle, &count, &extensions);
        if (DlssNative.Failed(result))
        {
            LogOff($"the GPU or its driver has no DLSS (NGX result 0x{result:X8})");
            return null;
        }

        string[] names = GetNames(extensions, count);
        string? missing = Array.Find(names, name => !vk.IsDeviceExtensionPresent(physicalDevice, name));
        if (missing is not null)
        {
            LogOff($"NGX needs the device extension {missing}, which the GPU lacks");
            return null;
        }

        _logger.LogDebug("NGX needs the device extensions {Extensions}.", names);
        return Array.FindAll(names, name => name != BufferDeviceAddressExtension);
    }

    /// <summary>Starts NGX on the device and checks DLSS runs there; otherwise logs why not and returns false.</summary>
    public bool Start(Vk vk, Instance instance, PhysicalDevice physicalDevice, Device device)
    {
        nint getInstanceProcAddr = vk.Context.GetProcAddress("vkGetInstanceProcAddr");
        nint getDeviceProcAddr = vk.Context.GetProcAddress("vkGetDeviceProcAddr");
        DlssApplication application = _application;
        nint context = 0;
        uint result = DlssNative.FvDlssInit(&application, instance.Handle, physicalDevice.Handle, device.Handle, getInstanceProcAddr, getDeviceProcAddr, &context);
        if (DlssNative.Failed(result))
        {
            LogOff($"NGX did not start DLSS on the GPU (NGX result 0x{result:X8}); its log is in {DataPath}");
            return false;
        }

        _context = context;
        _logger.LogInformation("DLSS is on; NGX's log is in {DataPath}.", DataPath);
        return true;
    }

    /// <summary>The size a mode draws the scene at for an output's.</summary>
    public (int Width, int Height) GetRenderSize(UpscaleMode mode, int outputWidth, int outputHeight)
    {
        DlssMode dlssMode = ToDlss(mode);
        uint width = 0;
        uint height = 0;
        uint result = DlssNative.FvDlssRenderSize(_context, dlssMode, (uint)outputWidth, (uint)outputHeight, &width, &height);
        Check(result, "find the size to draw at");
        return ((int)width, (int)height);
    }

    /// <summary>Records making a DLSS feature, which must run before the feature evaluates.</summary>
    public nint CreateFeature(CommandBuffer commands, UpscaleMode mode, int renderWidth, int renderHeight, int outputWidth, int outputHeight)
    {
        DlssMode dlssMode = ToDlss(mode);
        nint feature = 0;
        uint result = DlssNative.FvDlssCreate(_context, commands.Handle, dlssMode, (uint)renderWidth, (uint)renderHeight, (uint)outputWidth, (uint)outputHeight, &feature);
        Check(result, "make its feature");
        return feature;
    }

    /// <summary>Records upscaling a frame, which leaves NGX's own pipeline and descriptor sets bound.</summary>
    public void Evaluate(CommandBuffer commands, nint feature, DlssFrame frame)
    {
        uint result = DlssNative.FvDlssEvaluate(_context, commands.Handle, feature, &frame);
        Check(result, "upscale");
    }

    /// <summary>Releases a feature once the GPU has finished with it.</summary>
    public void ReleaseFeature(nint feature)
    {
        uint result = DlssNative.FvDlssRelease(feature);
        if (DlssNative.Failed(result))
        {
            _logger.LogWarning("DLSS could not release a feature (NGX result 0x{Result:X8}).", result);
        }
    }

    /// <summary>Stops NGX on the device, after its features are released and before the device is destroyed.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_context != 0)
        {
            uint result = DlssNative.FvDlssShutdown(_context);
            _context = 0;
            if (DlssNative.Failed(result))
            {
                _logger.LogWarning("NGX did not shut down (NGX result 0x{Result:X8}).", result);
            }
        }

        foreach (nint text in _strings)
        {
            Marshal.FreeCoTaskMem(text);
        }
    }

    private static string[] GetNames(ExtensionProperties* extensions, uint count)
    {
        string[] names = new string[count];
        for (int index = 0; index < count; index++)
        {
            byte* name = extensions[index].ExtensionName;
            names[index] = Marshal.PtrToStringUTF8((nint)name) ?? string.Empty;
        }

        return names;
    }

    private static DlssMode ToDlss(UpscaleMode mode)
    {
        switch (mode)
        {
            case UpscaleMode.Quality:
                return DlssMode.Quality;
            case UpscaleMode.Dlaa:
                return DlssMode.Dlaa;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "DLSS upscales in Quality or DLAA.");
        }
    }

    private void Check(uint result, string action)
    {
        if (DlssNative.Failed(result))
        {
            throw new InvalidOperationException($"DLSS could not {action} (NGX result 0x{result:X8}); NGX's log is in {DataPath}.");
        }
    }

    private void LogOff(string reason)
    {
        _logger.LogInformation("DLSS is off: {Reason}.", reason);
    }
}
