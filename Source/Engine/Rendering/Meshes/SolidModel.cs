using System.Numerics;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>Draws a solid as a greybox is drawn: its triangles as one grey part, each face flat, its texture coordinates in metres.</summary>
public static class SolidModel
{
    public static readonly Vector4 Color = new(0.6f, 0.6f, 0.6f, 1);

    public static ModelData Create(SolidMesh solid)
    {
        SolidTriangles triangles = solid.Triangulate();
        MeshVertex[] vertices = new MeshVertex[triangles.Vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            SolidVertex corner = triangles.Vertices[i];
            vertices[i] = new MeshVertex(corner.Position, corner.Normal, corner.TextureCoordinates);
        }

        MaterialData material = new(Color, null);
        MeshPart part = new(vertices, triangles.Indices, material);
        return new ModelData([part], []);
    }
}
