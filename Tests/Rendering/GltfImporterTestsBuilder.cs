using System.Numerics;

using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Memory;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>Builds glTF models in memory and imports them: a triangle facing +Z, wound counter-clockwise as glTF requires.</summary>
internal sealed class GltfImporterTestsBuilder
{
    /// <summary>A 2x1 PNG: a red pixel, then a blue one.</summary>
    public const string RedBluePng = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAADklEQVR4nGP4z8AAQv8BD/kD/YURmXYAAAAASUVORK5CYII=";

    private readonly List<(Vector3 Position, Vector2 Uv)[]> _triangles = [];
    private Matrix4x4 _nodeTransform = Matrix4x4.Identity;
    private Vector4 _baseColor = Vector4.One;
    private bool _textured;

    public GltfImporterTestsBuilder WithTriangle()
    {
        _triangles.Add([(Vector3.Zero, Vector2.Zero), (Vector3.UnitX, Vector2.UnitX), (Vector3.UnitY, Vector2.UnitY)]);
        return this;
    }

    /// <summary>Adds two triangles covering the square from -size to size in X and Y, facing +Z, with uvs from top left.</summary>
    public GltfImporterTestsBuilder WithQuad(float size)
    {
        (Vector3, Vector2) bottomLeft = (new Vector3(-size, -size, 0), new Vector2(0, 1));
        (Vector3, Vector2) bottomRight = (new Vector3(size, -size, 0), new Vector2(1, 1));
        (Vector3, Vector2) topRight = (new Vector3(size, size, 0), new Vector2(1, 0));
        (Vector3, Vector2) topLeft = (new Vector3(-size, size, 0), new Vector2(0, 0));
        _triangles.Add([bottomLeft, bottomRight, topRight]);
        _triangles.Add([bottomLeft, topRight, topLeft]);
        return this;
    }

    public GltfImporterTestsBuilder WithNodeTransform(Matrix4x4 transform)
    {
        _nodeTransform = transform;
        return this;
    }

    public GltfImporterTestsBuilder WithBaseColor(Vector4 color)
    {
        _baseColor = color;
        return this;
    }

    public GltfImporterTestsBuilder WithRedBlueTexture()
    {
        _textured = true;
        return this;
    }

    public ModelRoot BuildModel()
    {
        MaterialBuilder material = new("Material");
        if (_textured)
        {
            byte[] png = Convert.FromBase64String(RedBluePng);
            MemoryImage memoryImage = new(png);
            ImageBuilder image = ImageBuilder.From(memoryImage, "RedBlue");
            material.WithBaseColor(image, _baseColor);
        }
        else
        {
            material.WithBaseColor(_baseColor);
        }

        MeshBuilder<VertexPositionNormal, VertexTexture1> mesh = new("Mesh");
        PrimitiveBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexEmpty> primitive = mesh.UsePrimitive(material);
        foreach ((Vector3 Position, Vector2 Uv)[] triangle in _triangles)
        {
            primitive.AddTriangle(Vertex(triangle[0]), Vertex(triangle[1]), Vertex(triangle[2]));
        }

        SceneBuilder scene = new();
        scene.AddRigidMesh(mesh, _nodeTransform);
        return scene.ToGltf2();
    }

    public IReadOnlyList<MeshPart> Build()
    {
        ModelRoot model = BuildModel();
        return GltfImporter.Import(model);
    }

    /// <summary>The first triangle's corners in index order.</summary>
    public static MeshVertex[] Corners(MeshPart part)
    {
        return [part.Vertices[part.Indices[0]], part.Vertices[part.Indices[1]], part.Vertices[part.Indices[2]]];
    }

    private static VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> Vertex((Vector3 Position, Vector2 Uv) corner)
    {
        VertexPositionNormal geometry = new(corner.Position, Vector3.UnitZ);
        VertexTexture1 material = new(corner.Uv);
        return new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(geometry, material);
    }
}
