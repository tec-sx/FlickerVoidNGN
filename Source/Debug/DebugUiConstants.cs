using System.Numerics;
using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Debug;

/// <summary>The push constants of Shaders/DebugUi.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct DebugUiConstants(Vector2 Scale, Vector2 Translate, uint Vertices, uint Texture, uint Sampler, uint LinearOutput);
