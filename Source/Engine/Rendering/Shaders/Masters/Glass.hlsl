// The Glass master: the painting as Painted reads it is the glass's tint, and a painted layer on it is paint, whose coverage is
// the surface's alpha (ShadingModel.Glass, drawn by Transparent.hlsl).

Surface SurfaceOf(VertexOutput input, Draw draw, Material material, MasterParameters parameters)
{
    Surface surface = ReadSurface(input, draw, material);
    float3 paint = parameters.PaintTint;
    float amount = parameters.PaintAmount;
    if (parameters.Paint != None)
    {
        SamplerState sampler = Samplers[draw.Sampler];
        float4 painted = Textures[NonUniformResourceIndex(parameters.Paint)].Sample(sampler, input.Uv * parameters.PaintScale);
        paint *= painted.rgb;
        amount *= painted.a;
    }

    amount = saturate(amount);
    surface.Albedo = float4(lerp(surface.Albedo.rgb, paint, amount), amount);
    return surface;
}
