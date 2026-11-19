using System.Numerics;

using SharpGLTF.Memory;
using SharpGLTF.Schema2;

using StbImageSharp;

namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// Reads glTF models into mesh parts, one per primitive, with each node's transform baked in. Skinned meshes stay in their
/// bind pose with their joints and weights, and the animations of the model's skin are baked into <see cref="AnimationClip"/>s;
/// a model has at most one skin.
/// </summary>
public static class GltfImporter
{
    public static ModelData Load(string path)
    {
        ModelRoot model = ModelRoot.Load(path);
        return Import(model);
    }

    public static ModelData Import(ModelRoot model)
    {
        if (model.LogicalSkins.Count > 1)
        {
            throw new NotSupportedException($"The model has {model.LogicalSkins.Count} skins; only one is supported.");
        }

        List<MeshPart> parts = [];
        IEnumerable<Node> nodes = Node.Flatten(model.DefaultScene);
        foreach (Node node in nodes)
        {
            if (node.Mesh is null)
            {
                continue;
            }

            // glTF places skinned meshes by their joints alone, ignoring the node's own transform.
            bool skinned = node.Skin is not null;
            Matrix4x4 world = node.WorldMatrix;
            if (skinned)
            {
                world = Matrix4x4.Identity;
            }

            foreach (MeshPrimitive primitive in node.Mesh.Primitives)
            {
                MeshPart? part = ImportPrimitive(primitive, world, skinned);
                if (part is not null)
                {
                    parts.Add(part);
                }
            }
        }

        List<AnimationClip> animations = [];
        if (model.LogicalSkins.Count == 1)
        {
            Skin skin = model.LogicalSkins[0];
            foreach (Animation animation in model.LogicalAnimations)
            {
                AnimationClip clip = BakeAnimation(skin, animation);
                animations.Add(clip);
            }
        }

        return new ModelData(parts, animations);
    }

    /// <summary>Returns null for primitives without triangles, such as lines and points.</summary>
    private static MeshPart? ImportPrimitive(MeshPrimitive primitive, Matrix4x4 world, bool skinned)
    {
        uint[] indices = ImportIndices(primitive, world);
        if (indices.Length == 0)
        {
            return null;
        }

        MeshVertex[] vertices = ImportVertices(primitive, world);
        MaterialData material = ImportMaterial(primitive.Material);
        SkinVertex[]? skin = null;
        if (skinned)
        {
            skin = ImportSkin(primitive);
        }

        return new MeshPart(vertices, indices, material) { Skin = skin };
    }

    private static SkinVertex[] ImportSkin(MeshPrimitive primitive)
    {
        IAccessorArray<Vector4> joints = primitive.GetVertexAccessor("JOINTS_0").AsVector4Array();
        IAccessorArray<Vector4> weights = primitive.GetVertexAccessor("WEIGHTS_0").AsVector4Array();
        SkinVertex[] skin = new SkinVertex[joints.Count];
        for (int index = 0; index < skin.Length; index++)
        {
            Vector4 joint = joints[index];
            Vector4 weight = weights[index];
            float total = weight.X + weight.Y + weight.Z + weight.W;
            if (total > 0)
            {
                weight /= total;
            }

            skin[index] = new SkinVertex((uint)joint.X, (uint)joint.Y, (uint)joint.Z, (uint)joint.W, weight);
        }

        return skin;
    }

    // Each joint's matrix takes a vertex from the bind pose, through the joint's inverse bind matrix, to where the joint is now.
    private static AnimationClip BakeAnimation(Skin skin, Animation animation)
    {
        int jointCount = skin.JointsCount;
        int frameCount = (int)MathF.Ceiling(animation.Duration * AnimationClip.FrameRate) + 1;
        Matrix4x4[] frames = new Matrix4x4[frameCount * jointCount];
        for (int frame = 0; frame < frameCount; frame++)
        {
            float time = MathF.Min(frame / AnimationClip.FrameRate, animation.Duration);
            for (int joint = 0; joint < jointCount; joint++)
            {
                Node node = skin.Joints[joint];
                Matrix4x4 jointWorld = node.GetWorldMatrix(animation, time);
                frames[(frame * jointCount) + joint] = skin.InverseBindMatrices[joint] * jointWorld;
            }
        }

        return new AnimationClip(animation.Name, animation.Duration, jointCount, frames);
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
