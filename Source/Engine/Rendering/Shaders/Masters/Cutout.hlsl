// The Cutout master's surface: the painting as Painted reads it; the master draws both sides of its cards.

Surface SurfaceOf(VertexOutput input, Draw draw, Material material, MasterParameters parameters)
{
    return ReadSurface(input, draw, material);
}
