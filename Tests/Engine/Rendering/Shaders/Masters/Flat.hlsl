// The test master's surface: the scene shader's, painted its flat color.

Surface SurfaceOf(VertexOutput input, Draw draw, Material material, MasterParameters parameters)
{
    Surface surface = ReadSurface(input, draw, material);
    surface.Albedo = float4(parameters.FlatColor, 1);
    surface.Impasto = parameters.Thickness;
    return surface;
}
