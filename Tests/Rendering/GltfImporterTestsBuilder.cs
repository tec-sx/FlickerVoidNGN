using System.Numerics;

using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Memory;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;

using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>
/// Builds glTF models in memory and imports them: triangles facing +Z, wound counter-clockwise as glTF requires. A moving
/// joint makes the model skinned: a root joint at the origin and a child joint that every vertex follows fully.
/// </summary>
internal sealed class GltfImporterTestsBuilder
{
    /// <summary>A 2x1 PNG: a red pixel, then a blue one.</summary>
    /// <summary>A 2x1 PNG: a blue pixel, then a red one.</summary>
    public const string BlueRedPng = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAAEElEQVR4nGNgYPj//z+QAAAP+QP9HTMzcAAAAABJRU5ErkJggg==";

    public const string RedBluePng = "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAADklEQVR4nGP4z8AAQv8BD/kD/YURmXYAAAAASUVORK5CYII=";

    private readonly List<(Vector3 Position, Vector2 Uv)[]> _triangles = [];
    private Matrix4x4 _nodeTransform = Matrix4x4.Identity;
    private Vector4 _baseColor = Vector4.One;
    private bool _textured;
    private (Vector3 From, Vector3 To, float Seconds)? _movingJoint;

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

    /// <summary>Skins the model to a joint that an animation called Move slides from one offset to another.</summary>
    public GltfImporterTestsBuilder WithMovingJoint(Vector3 from, Vector3 to, float seconds)
    {
        _movingJoint = (from, to, seconds);
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

        SceneBuilder scene = new();
        if (_movingJoint is { } joint)
        {
            NodeBuilder root = new("Root");
            NodeBuilder moving = root.CreateNode("Moving");
            moving.UseTranslation("Move").WithPoint(0, joint.From).WithPoint(joint.Seconds, joint.To);
            VertexJoints4 skin = new(1);
            MeshBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4> skinnedMesh = BuildMesh(material, skin);
            scene.AddSkinnedMesh(skinnedMesh, Matrix4x4.Identity, root, moving);
        }
        else
        {
            MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> mesh = BuildMesh(material, default(VertexEmpty));
            scene.AddRigidMesh(mesh, _nodeTransform);
        }

        return scene.ToGltf2();
    }

    public ModelData BuildData()
    {
        ModelRoot model = BuildModel();
        return GltfImporter.Import(model);
    }

    public IReadOnlyList<MeshPart> Build()
    {
        ModelData data = BuildData();
        return data.Parts;
    }

    /// <summary>The first triangle's corners in index order.</summary>
    public static MeshVertex[] Corners(MeshPart part)
    {
        return [part.Vertices[part.Indices[0]], part.Vertices[part.Indices[1]], part.Vertices[part.Indices[2]]];
    }

    private MeshBuilder<VertexPositionNormal, VertexTexture1, TSkin> BuildMesh<TSkin>(MaterialBuilder material, TSkin skin)
        where TSkin : struct, IVertexSkinning
    {
        MeshBuilder<VertexPositionNormal, VertexTexture1, TSkin> mesh = new("Mesh");
        PrimitiveBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, TSkin> primitive = mesh.UsePrimitive(material);
        foreach ((Vector3 Position, Vector2 Uv)[] triangle in _triangles)
        {
            primitive.AddTriangle(Vertex(triangle[0], skin), Vertex(triangle[1], skin), Vertex(triangle[2], skin));
        }

        return mesh;
    }

    private static VertexBuilder<VertexPositionNormal, VertexTexture1, TSkin> Vertex<TSkin>((Vector3 Position, Vector2 Uv) corner, TSkin skin)
        where TSkin : struct, IVertexSkinning
    {
        VertexPositionNormal geometry = new(corner.Position, Vector3.UnitZ);
        VertexTexture1 material = new(corner.Uv);
        return new VertexBuilder<VertexPositionNormal, VertexTexture1, TSkin>(geometry, material, skin);
    }
}
