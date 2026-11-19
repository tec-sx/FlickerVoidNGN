using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering;

/// <summary>The push constants of Shaders/Skinning.hlsl, for one skinned mesh part.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SkinningConstants(uint Source, uint Skin, uint Joints, uint JointsOffset, uint Output, uint VertexCount);
