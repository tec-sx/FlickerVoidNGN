using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// The push constants of Shaders/Lighting.hlsl, for its lighting kernel (the frame buffer and the HDR target) and its fog volume
/// kernels (the frame buffer, the fog's buffers: the volume lit this frame, the one before's, and the integrated volume, and
/// whether the one before's is kept, with the frame's number, and the fog's own data: its noise and fog volumes, GpuFog), the
/// renderer's time, which rain ripples move by, and whether the lighting kernel draws the flat look.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct LightingConstants(uint Frame, uint Output, uint Lit, uint History, uint Integrated, uint KeepsHistory, uint Number, uint Fog, float Time, uint Flat = 0)
{
    public LightingConstants(uint frame, uint output, float time, bool flat)
        : this(frame, output, 0, 0, 0, 0, 0, 0, time, flat ? 1u : 0u)
    {
    }
}
