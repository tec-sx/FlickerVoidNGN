using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Graphics.Vulkan.Upscaling;

/// <summary>A frame for DLSS to upscale (FvDlssFrame): its images, the jitter in render pixels, the render size and whether history starts afresh.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DlssFrame
{
    public DlssImage Color;
    public DlssImage Depth;
    public DlssImage Motion;
    public DlssImage Exposure;
    public DlssImage Output;
    public float JitterX;
    public float JitterY;
    public uint RenderWidth;
    public uint RenderHeight;
    public int Reset;
}
