using System.Numerics;

using FlickerVoid.Engine.Editor.Materials;
using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Editor.Tests.Materials;

public sealed class PreviewMeshesTests
{
    [Fact]
    public void BuildsASphereWithNormalsPointingOutward()
    {
        ModelData sphere = PreviewMeshes.CreateSphere(radius: 0.8f, rings: 8, segments: 12);

        MeshPart part = sphere.Parts.Should().ContainSingle().Subject;
        part.Indices.Should().HaveCount(8 * 12 * 6);
        foreach (MeshVertex vertex in part.Vertices)
        {
            vertex.Position.Length().Should().BeApproximately(0.8f, 1e-4f);
            Vector3 outward = Vector3.Normalize(vertex.Position);
            Vector3.Dot(vertex.Normal, outward).Should().BeApproximately(1, 1e-4f);
        }
    }

    [Fact]
    public void BuildsACubeWithAFlatNormalPerFace()
    {
        ModelData cube = PreviewMeshes.CreateCube(size: 1);

        MeshPart part = cube.Parts.Should().ContainSingle().Subject;
        part.Vertices.Should().HaveCount(24);
        part.Indices.Should().HaveCount(36);
        foreach (MeshVertex vertex in part.Vertices)
        {
            float alongNormal = Vector3.Dot(vertex.Position, vertex.Normal);
            alongNormal.Should().BeApproximately(0.5f, 1e-5f, "each vertex lies on the face its normal points out of");
        }
    }

    [Fact]
    public void WindsTrianglesTheWayTheRendererKeepsAsFrontFaces()
    {
        ModelData cube = PreviewMeshes.CreateCube(size: 1);
        ModelData sphere = PreviewMeshes.CreateSphere(radius: 1, rings: 8, segments: 12);

        CheckWinding(cube.Parts[0]);
        CheckWinding(sphere.Parts[0]);
    }

    private static void CheckWinding(MeshPart part)
    {
        for (int index = 0; index < part.Indices.Length; index += 3)
        {
            MeshVertex a = part.Vertices[part.Indices[index]];
            MeshVertex b = part.Vertices[part.Indices[index + 1]];
            MeshVertex c = part.Vertices[part.Indices[index + 2]];
            Vector3 face = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
            if (face.LengthSquared() < 1e-10f)
            {
                continue;
            }

            Vector3 normal = a.Normal + b.Normal + c.Normal;
            Vector3.Dot(face, normal).Should().BeLessThan(0, "front faces wind like the importer's, clockwise seen from outside");
        }
    }
}
