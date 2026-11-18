using Silk.NET.Core.Native;

namespace FlickerVoid.Engine.Graphics.Direct3D12;

/// <summary>Checks HRESULTs and releases COM pointers.</summary>
internal static unsafe class D3D12Com
{
    public static void Check(int result)
    {
        SilkMarshal.ThrowHResult(result);
    }

    public static void Release(void* pointer)
    {
        if (pointer != null)
        {
            ((IUnknown*)pointer)->Release();
        }
    }
}
