using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// A fog volume as the fog kernels read it (Lighting.hlsl's FogVolumeDensity): its box's centre and densest density, its three
/// axes in the world with its half size along each, its colour and edge softness, and its height falloff, noise share and shape.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GpuFogVolume(
    Vector3 Center,
    float Density,
    Vector3 AxisX,
    float HalfX,
    Vector3 AxisY,
    float HalfY,
    Vector3 AxisZ,
    float HalfZ,
    Vector3 Color,
    float EdgeSoftness,
    float HeightFalloff,
    float Noise,
    uint Shape,
    float Padding = 0)
{
    /// <summary>The volume's record, its box standing on its entity's position.</summary>
    public static GpuFogVolume From(Extraction.RenderFogVolume volume)
    {
        FogVolume fog = volume.Volume;
        Vector3 half = fog.Size / 2;
        Vector3 axisX = Vector3.Transform(Vector3.UnitX, volume.Rotation);
        Vector3 axisY = Vector3.Transform(Vector3.UnitY, volume.Rotation);
        Vector3 axisZ = Vector3.Transform(Vector3.UnitZ, volume.Rotation);
        Vector3 center = volume.Position + (axisY * half.Y);
        return new GpuFogVolume(center, fog.Density, axisX, half.X, axisY, half.Y, axisZ, half.Z, fog.Color, fog.EdgeSoftness, fog.HeightFalloff, fog.Noise, (uint)fog.Shape);
    }
}
