using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Outlines;

/// <summary>The push constants of Shaders/Outline.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct OutlineConstants(uint Mask, uint Width);
