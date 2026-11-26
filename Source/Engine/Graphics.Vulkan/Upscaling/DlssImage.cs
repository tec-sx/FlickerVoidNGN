using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Graphics.Vulkan.Upscaling;

/// <summary>An image DLSS reads or writes, by its Vulkan handles (FvDlssImage).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DlssImage
{
    public ulong Image;
    public ulong View;
    public uint Format;
    public uint Aspect;
    public uint Width;
    public uint Height;
}
