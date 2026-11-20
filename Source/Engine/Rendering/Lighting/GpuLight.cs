using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// A point or spot light as Shaders/Scene.hlsl reads it: 80 bytes. The color is scaled by the intensity; a point light's cone
/// cosines lie below -1, so its whole sphere is inside the cone. Cookie and Shadow are a bindless texture and the first
/// <see cref="GpuShadow"/> (six for a point light), or <see cref="None"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 80)]
internal readonly record struct GpuLight(
    Vector3 Position,
    float Range,
    Vector3 Color,
    float CosOuter,
    Vector3 Direction,
    float CosInner,
    Vector3 Right,
    uint Cookie,
    uint Shadow)
{
    public const uint None = uint.MaxValue;
}

