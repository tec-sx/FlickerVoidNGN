// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See THIRD-PARTY-NOTICES.md.
// Adapted from Stride's D3D12Runtime (Stride.Graphics/Direct3D12/D3D12Runtime.Direct3D12.cs).

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace FlickerVoid.Engine.Graphics.Direct3D12;

/// <summary>
/// Creates Direct3D 12 devices and debug interfaces through the Agility SDK copied to <c>D3D12\</c> next to the app,
/// falling back to the system runtime when it is missing or Windows can't load it (a Windows update from September 2023 or newer is needed).
/// </summary>
internal sealed unsafe class D3D12Runtime : IDisposable
{
    public static readonly Guid DebugClassId = new(0xf2352aeb, 0xdd84, 0x49fe, 0xb9, 0x7b, 0xa9, 0xdc, 0xfd, 0xcc, 0x1b, 0x4f);
    private static readonly Guid SdkConfigurationClassId = new(0x7cda6aca, 0xa03e, 0x49c8, 0x94, 0x58, 0x03, 0x34, 0xd2, 0x0e, 0x07, 0xce);

    private readonly D3D12 _d3d12;
    private readonly ID3D12DeviceFactory* _factory;

    public D3D12Runtime(D3D12 d3d12, ILogger logger)
    {
        _d3d12 = d3d12;
        _factory = CreateDeviceFactory(logger);
    }

    public int CreateDevice(IUnknown* adapter, Guid* interfaceId, void** device)
    {
        if (_factory != null)
        {
            return _factory->CreateDevice(adapter, D3DFeatureLevel.Level110, interfaceId, device);
        }

        return _d3d12.CreateDevice(adapter, D3DFeatureLevel.Level110, interfaceId, device);
    }

    /// <summary>Gets a debug interface that applies to the devices created after it is configured.</summary>
    public int GetDebugInterface(Guid classId, Guid* interfaceId, void** debug)
    {
        if (_factory != null)
        {
            return _factory->GetConfigurationInterface(&classId, interfaceId, debug);
        }

        return _d3d12.GetDebugInterface(interfaceId, debug);
    }

    public void Dispose()
    {
        D3D12Com.Release(_factory);
    }

    private ID3D12DeviceFactory* CreateDeviceFactory(ILogger logger)
    {
        string sdkPath = Path.Combine(AppContext.BaseDirectory, "D3D12") + Path.DirectorySeparatorChar;
        string corePath = Path.Combine(sdkPath, "D3D12Core.dll");
        if (!File.Exists(corePath))
        {
            logger.LogInformation("No Agility SDK at {Path}; using the system Direct3D 12 runtime.", sdkPath);
            return null;
        }

        // The factory accepts only the exact version of the DLL.
        if (!TryReadSdkVersion(corePath, out uint sdkVersion))
        {
            logger.LogWarning("{Path} has no D3D12SDKVersion export; using the system Direct3D 12 runtime.", corePath);
            return null;
        }

        Guid classId = SdkConfigurationClassId;
        Guid configurationId = ID3D12SDKConfiguration1.Guid;
        ID3D12SDKConfiguration1* configuration = null;
        int result = _d3d12.GetInterface(&classId, &configurationId, (void**)&configuration);
        if (result < 0)
        {
            logger.LogWarning("This Windows can't load the Agility SDK {Version} (0x{Result:X8}); using the system Direct3D 12 runtime.", sdkVersion, result);
            return null;
        }

        nint path = Marshal.StringToHGlobalAnsi(sdkPath);
        try
        {
            Guid factoryId = ID3D12DeviceFactory.Guid;
            ID3D12DeviceFactory* factory = null;
            result = configuration->CreateDeviceFactory(sdkVersion, (byte*)path, &factoryId, (void**)&factory);
            if (result < 0)
            {
                logger.LogWarning("Can't load the Agility SDK {Version} from {Path} (0x{Result:X8}); using the system Direct3D 12 runtime.", sdkVersion, sdkPath, result);
                return null;
            }

            logger.LogInformation("Using the Agility SDK {Version} from {Path}.", sdkVersion, sdkPath);
            return factory;
        }
        finally
        {
            Marshal.FreeHGlobal(path);
            configuration->Release();
        }
    }

    private static bool TryReadSdkVersion(string corePath, out uint sdkVersion)
    {
        sdkVersion = 0;
        if (!NativeLibrary.TryLoad(corePath, out nint module))
        {
            return false;
        }

        try
        {
            if (!NativeLibrary.TryGetExport(module, "D3D12SDKVersion", out nint address))
            {
                return false;
            }

            sdkVersion = *(uint*)address;
            return true;
        }
        finally
        {
            NativeLibrary.Free(module);
        }
    }
}
