using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Rendering.PostProcessing;

/// <summary>The mood's grade as shaders read it (Grading in Shaders/Grade.hlsl): exposure, contrast, saturation, tint and the tonemapper.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct Grading(Vector3 Tint, float Exposure, float Contrast, float Saturation, uint Tonemapper, float WhitePoint)
{
    public static Grading From(LightingMood mood)
    {
        return new Grading(mood.Tint, mood.Exposure, mood.Contrast, mood.Saturation, (uint)mood.Tonemapper, mood.WhitePoint);
    }
}
