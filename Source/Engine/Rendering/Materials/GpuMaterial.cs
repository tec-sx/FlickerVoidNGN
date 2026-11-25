using FlickerVoid.Engine.Graphics.Buffers;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// A material as drawn: the GPU buffer of its parameters, and whether it cuts holes where its paint is thin (its cutoff is above
/// 0), which shadows and ray tracing must know before they draw.
/// </summary>
internal readonly record struct GpuMaterial(IBuffer Parameters, bool Cutout);
