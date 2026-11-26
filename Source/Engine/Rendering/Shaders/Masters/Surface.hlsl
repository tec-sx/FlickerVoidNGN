// The Surface master: the painting as Painted reads it, its tint varied by place, under a second painted layer laid by a mask,
// by height above the ground and on what faces up.

#include "Layers.hlsli"

Surface SurfaceOf(VertexOutput input, Draw draw, Material material, MasterParameters parameters)
{
    Surface surface = ReadSurface(input, draw, material);
    surface.Albedo.rgb = VaryTint(surface.Albedo.rgb, input.WorldPosition, parameters.Variation, parameters.VariationScale);
    float amount = parameters.LayerAmount;
    if (parameters.LayerHeight > 0)
    {
        amount *= saturate(1 - input.WorldPosition.y / parameters.LayerHeight);
    }

    float up = saturate(surface.Normal.y);
    amount *= lerp(1, up, parameters.LayerFacingUp);
    SamplerState sampler = Samplers[draw.Sampler];
    surface.Albedo.rgb = LayOver(surface.Albedo.rgb, parameters.Layer, parameters.LayerTint, parameters.LayerScale, amount, sampler, input.Uv);
    return surface;
}
