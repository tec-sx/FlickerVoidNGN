// The Metal master: the painting as Painted reads it, its tint varied by place, under a painted layer of rust; the lighting pass
// lights it as metal by its metalness (ShadingModel.Metal).

#include "Layers.hlsli"

Surface SurfaceOf(VertexOutput input, Draw draw, Material material, MasterParameters parameters)
{
    Surface surface = ReadSurface(input, draw, material);
    surface.Albedo.rgb = VaryTint(surface.Albedo.rgb, input.WorldPosition, parameters.Variation, parameters.VariationScale);
    SamplerState sampler = Samplers[draw.Sampler];
    surface.Albedo.rgb = LayOver(surface.Albedo.rgb, parameters.Rust, parameters.RustTint, parameters.RustScale, parameters.RustAmount, sampler, input.Uv);
    return surface;
}
