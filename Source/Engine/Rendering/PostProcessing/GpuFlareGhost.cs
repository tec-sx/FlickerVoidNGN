using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>A flare ghost as Shaders/Flare.hlsl reads it: its colour, then its scale.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GpuFlareGhost(Vector3 Color, float Scale);
