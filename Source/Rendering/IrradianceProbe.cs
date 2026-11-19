using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// The light arriving at a point from every direction, as first-order spherical harmonics already convolved for diffuse
/// surfaces: per color channel, the constant term and the x, y and z terms. 48 bytes, as Shaders/Scene.hlsl reads it.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct IrradianceProbe(Vector4 Red, Vector4 Green, Vector4 Blue)
{
    /// <summary>A probe that gives every normal the same irradiance.</summary>
    public static IrradianceProbe Even(Vector3 irradiance)
    {
        Vector4 red = new(irradiance.X, 0, 0, 0);
        Vector4 green = new(irradiance.Y, 0, 0, 0);
        Vector4 blue = new(irradiance.Z, 0, 0, 0);
        return new IrradianceProbe(red, green, blue);
    }

    /// <summary>The irradiance divided by pi that a surface facing the normal gets, so a white surface reflects it as is.</summary>
    public Vector3 Evaluate(Vector3 normal)
    {
        Vector4 basis = new(1, normal.X, normal.Y, normal.Z);
        float red = Vector4.Dot(Red, basis);
        float green = Vector4.Dot(Green, basis);
        float blue = Vector4.Dot(Blue, basis);
        Vector3 irradiance = new(red, green, blue);
        return Vector3.Max(irradiance, Vector3.Zero);
    }
}
