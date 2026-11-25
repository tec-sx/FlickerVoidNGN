using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>The push constants of Shaders/Scene.hlsl: the frame's buffer and the number of the draw's record in it.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SceneConstants(uint Frame, uint Draw);
