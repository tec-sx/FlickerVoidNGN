using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering;

/// <summary>The push constants of Shaders/Scene.hlsl, for one mesh part.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SceneConstants(
    Matrix4x4 WorldViewProjection,
    Vector3 SunDirection,
    uint Vertices,
    uint Texture,
    uint Sampler,
    float Ambient,
    uint Padding,
    Vector4 BaseColor);
