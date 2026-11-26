// The scene shader (SceneSurface.hlsl): the geometry pass of the materials without a master, which read their surface as the
// Painted master does, and the vertex and outline stages every master shares.

#include "SceneSurface.hlsl"

Surface SurfaceAt(VertexOutput input, Draw draw, Material material)
{
    return ReadSurface(input, draw, material);
}

#include "Geometry.hlsl"
