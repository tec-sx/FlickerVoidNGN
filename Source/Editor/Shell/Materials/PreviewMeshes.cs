using System.Numerics;

using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Editor.Materials;

/// <summary>
/// The light grey sphere and cube the material editor previews materials on, wound like imported models: clockwise seen
/// from outside. Grey rather than white leaves room for light and rim before the image clips.
/// </summary>
public static class PreviewMeshes
{
    public static ModelData CreateSphere(float radius, int rings, int segments)
    {
        List<MeshVertex> vertices = [];
        for (int ring = 0; ring <= rings; ring++)
        {
            float v = ring / (float)rings;
            float polar = v * MathF.PI;
            for (int segment = 0; segment <= segments; segment++)
            {
                float u = segment / (float)segments;
                float azimuth = u * MathF.Tau;
                Vector3 normal = new(MathF.Sin(polar) * MathF.Cos(azimuth), MathF.Cos(polar), MathF.Sin(polar) * MathF.Sin(azimuth));
                vertices.Add(new MeshVertex(normal * radius, normal, new Vector2(u, v)));
            }
        }

        List<uint> indices = [];
        int stride = segments + 1;
        for (int ring = 0; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                uint topLeft = (uint)((ring * stride) + segment);
                uint topRight = topLeft + 1;
                uint bottomLeft = topLeft + (uint)stride;
                uint bottomRight = bottomLeft + 1;
                indices.AddRange([topLeft, bottomLeft, topRight, topRight, bottomLeft, bottomRight]);
            }
        }

        return Create([.. vertices], [.. indices]);
    }

    public static ModelData CreateCube(float size)
    {
        Vector3[] normals = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        List<MeshVertex> vertices = [];
        List<uint> indices = [];
        float half = size / 2;
        foreach (Vector3 normal in normals)
        {
            Vector3 side = GetSide(normal);
            Vector3 up = Vector3.Cross(normal, side);
            uint first = (uint)vertices.Count;
            Vector3 centre = normal * half;
            vertices.Add(new MeshVertex(centre - (side * half) - (up * half), normal, new Vector2(0, 1)));
            vertices.Add(new MeshVertex(centre + (side * half) - (up * half), normal, new Vector2(1, 1)));
            vertices.Add(new MeshVertex(centre + (side * half) + (up * half), normal, new Vector2(1, 0)));
            vertices.Add(new MeshVertex(centre - (side * half) + (up * half), normal, new Vector2(0, 0)));
            indices.AddRange([first, first + 2, first + 1, first, first + 3, first + 2]);
        }

        return Create([.. vertices], [.. indices]);
    }

    /// <summary>A direction along the face, picked so side, up and normal turn the same way on every face.</summary>
    private static Vector3 GetSide(Vector3 normal)
    {
        if (MathF.Abs(normal.Y) > 0.5f)
        {
            return Vector3.UnitX;
        }

        return Vector3.Cross(Vector3.UnitY, normal);
    }

    private static ModelData Create(MeshVertex[] vertices, uint[] indices)
    {
        MaterialData grey = new(new Vector4(0.6f, 0.6f, 0.6f, 1), null);
        MeshPart part = new(vertices, indices, grey);
        return new ModelData([part], []);
    }
}
