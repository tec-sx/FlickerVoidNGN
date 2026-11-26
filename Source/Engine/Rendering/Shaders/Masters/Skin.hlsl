// The Skin master: the painting as Painted reads it, its tint varied by place; the lighting pass scatters light under it
// (ShadingModel.Skin).

#include "Layers.hlsli"

Surface SurfaceOf(VertexOutput input, Draw draw, Material material, MasterParameters parameters)
{
    Surface surface = ReadSurface(input, draw, material);
    surface.Albedo.rgb = VaryTint(surface.Albedo.rgb, input.WorldPosition, parameters.Variation, parameters.VariationScale);
    return surface;
}
