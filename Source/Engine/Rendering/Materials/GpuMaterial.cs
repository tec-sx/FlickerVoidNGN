using FlickerVoid.Engine.Graphics.Buffers;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// A material as drawn: the GPU buffer of its parameters, whether it cuts holes where its paint is thin (its cutoff is above
/// 0), which shadows and ray tracing must know before they draw, and the content path of its master, whose geometry pass draws
/// it, none for the scene shader's own, and whether it is glass, which the transparent pass draws over the lit scene in place of
/// the geometry pass, and shadows and rays pass through.
/// </summary>
internal readonly record struct GpuMaterial(IBuffer Parameters, bool Cutout, string? Master = null, bool Transparent = false);
