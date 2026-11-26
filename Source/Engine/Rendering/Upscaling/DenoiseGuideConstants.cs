using System.Runtime.InteropServices;

namespace FlickerVoid.Engine.Rendering.Upscaling;

/// <summary>The push constants of Shaders/DenoiseGuides.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct DenoiseGuideConstants(uint Frame, uint DiffuseAlbedo, uint SpecularAlbedo, uint NormalRoughness);
