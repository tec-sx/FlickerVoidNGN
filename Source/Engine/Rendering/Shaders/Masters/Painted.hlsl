// The Painted master's surface: the mesh part's painting and base color, tinted, its normal tilted by the painting's relief, as
// the scene shader reads every material without a master.

Surface SurfaceOf(VertexOutput input, Draw draw, Material material, MasterParameters parameters)
{
    return ReadSurface(input, draw, material);
}
