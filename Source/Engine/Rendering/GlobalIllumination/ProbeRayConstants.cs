using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.GlobalIllumination;

/// <summary>
/// The push constants of the probe ray passes (Shaders/ProbeRays.hlsl): the frame buffer, the rays' buffer, the probes' buffer, how
/// many rays each probe traces, the frame's number, which turns its rays, and how much of the probes' history the update keeps.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ProbeRayConstants(uint Frame, uint Rays, uint Probes, uint RayCount, uint Number, float Hysteresis);
