// The Glow master's surface: the painting as Painted reads it; the lighting pass makes it shine (ShadingModel.Glow).

Surface SurfaceOf(VertexOutput input, Draw draw, Material material, MasterParameters parameters)
{
    return ReadSurface(input, draw, material);
}
