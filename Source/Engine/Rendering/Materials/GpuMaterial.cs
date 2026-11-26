using FlickerVoid.Engine.Graphics.Buffers;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// A material as drawn: the GPU buffer of its parameters, whether it cuts holes where its paint is thin (its cutoff is above
/// 0), which shadows and ray tracing must know before they draw, and the content path of its master, whose geometry pass draws
/// it, none for the scene shader's own.
/// </summary>
internal readonly record struct GpuMaterial(IBuffer Parameters, bool Cutout, string? Master = null);
