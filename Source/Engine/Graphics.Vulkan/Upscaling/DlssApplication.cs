using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Graphics.Vulkan.Upscaling;

/// <summary>Who runs DLSS, as UTF-8 strings (FvDlssApplication): NGX's project id, the engine's version, and its data and runtime folders.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DlssApplication
{
    public byte* ProjectId;
    public byte* EngineVersion;
    public byte* DataPath;
    public byte* RuntimePath;
}
