using System.Numerics;

using SharpGLTF.Memory;
using SharpGLTF.Schema2;

using StbImageSharp;

namespace FlickerVoid.Engine.Rendering;

/// <summary>Reads glTF models into mesh parts, one per primitive, with each node's transform baked in.</summary>
public static class GltfImporter
{
    public static IReadOnlyList<MeshPart> Load(string path)
    {
        ModelRoot model = ModelRoot.Load(path);
        return Import(model);
    }

    public static IReadOnlyList<MeshPart> Import(ModelRoot model)
    {
        List<MeshPart> parts = [];
        IEnumerable<Node> nodes = Node.Flatten(model.DefaultScene);
        foreach (Node node in nodes)
        {
            if (node.Mesh is null)
            {
                continue;
            }

            Matrix4x4 world = node.WorldMatrix;
            foreach (MeshPrimitive primitive in node.Mesh.Primitives)
            {
                MeshPart? part = ImportPrimitive(primitive, world);
                if (part is not null)
                {
                    parts.Add(part);
                }
            }
        }

        return parts;
    }

    /// <summary>Returns null for primitives without triangles, such as lines and points.</summary>
    private static MeshPart? ImportPrimitive(MeshPrimitive primitive, Matrix4x4 world)
    {
        uint[] indices = ImportIndices(primitive, world);
        if (indices.Length == 0)
        {
            return null;
        }

        MeshVertex[] vertices = ImportVertices(primitive, world);
        MaterialData material = ImportMaterial(primitive.Material);
        return new MeshPart(vertices, indices, material);
    }

    private static MeshVertex[] ImportVertices(MeshPrimitive primitive, Matrix4x4 world)
    {
        IAccessorArray<Vector3> positions = primitive.GetVertexAccessor("POSITION").AsVector3Array();
        Accessor? normalAccessor = primitive.GetVertexAccessor("NORMAL");
        Accessor? uvAccessor = primitive.GetVertexAccessor("TEXCOORD_0");
        IAccessorArray<Vector3>? normals = normalAccessor?.AsVector3Array();
        IAccessorArray<Vector2>? uvs = uvAccessor?.AsVector2Array();

        Matrix4x4.Invert(world, out Matrix4x4 inverse);
        Matrix4x4 normalMatrix = Matrix4x4.Transpose(inverse);

        MeshVertex[] vertices = new MeshVertex[positions.Count];
        for (int index = 0; index < vertices.Length; index++)
        {
            Vector3 position = Vector3.Transform(positions[index], world);
            Vector3 normal = Vector3.Zero;
            if (normals is not null)
            {
                Vector3 transformed = Vector3.TransformNormal(normals[index], normalMatrix);
                normal = Vector3.Normalize(transformed);
            }

            Vector2 uv = Vector2.Zero;
            if (uvs is not null)
            {
                uv = uvs[index];
            }

            vertices[index] = new MeshVertex(position, normal, uv);
        }

        return vertices;
    }

    /// <summary>glTF winds front faces counter-clockwise and the engine clockwise, so triangles turn round unless the transform mirrors them already.</summary>
    private static uint[] ImportIndices(MeshPrimitive primitive, Matrix4x4 world)
    {
        bool mirrored = world.GetDeterminant() < 0;
        List<uint> indices = [];
        foreach ((int a, int b, int c) in primitive.GetTriangleIndices())
        {
            if (mirrored)
            {
                indices.AddRange([(uint)a, (uint)b, (uint)c]);
            }
            else
            {
                indices.AddRange([(uint)a, (uint)c, (uint)b]);
            }
        }

        return [.. indices];
    }

    private static MaterialData ImportMaterial(Material? material)
    {
        MaterialChannel? channel = material?.FindChannel("BaseColor");
        if (channel is null)
        {
            return new MaterialData(Vector4.One, null);
        }

        TextureData? texture = null;
        Image? image = channel.Value.Texture?.PrimaryImage;
        if (image is not null)
        {
            MemoryImage content = image.Content;
            texture = Decode(content);
        }

        return new MaterialData(channel.Value.Color, texture);
    }

    private static TextureData Decode(MemoryImage image)
    {
        byte[] bytes = image.Content.ToArray();
        ImageResult decoded = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha);
        return new TextureData(decoded.Width, decoded.Height, decoded.Data);
    }
}
