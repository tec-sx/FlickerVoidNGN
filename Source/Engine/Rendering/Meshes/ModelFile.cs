using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Materials;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Reads and writes a model as the content build bakes it (<c>Name.fvmodel</c>): its parts' vertices, indices, skins and
/// materials with their textures as DDS images, then its animation clips, all little-endian.
/// </summary>
public static class ModelFile
{
    private const uint Magic = 0x444D5646;
    private const int Version = 2;

    public static void Write(ModelData model, Stream stream)
    {
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(model.Parts.Count);
        foreach (MeshPart part in model.Parts)
        {
            WriteArray<MeshVertex>(writer, part.Vertices);
            WriteArray<uint>(writer, part.Indices);
            writer.Write(part.Skin is not null);
            if (part.Skin is not null)
            {
                WriteArray<SkinVertex>(writer, part.Skin);
            }

            WriteMaterial(writer, part.Material);
            writer.Write(part.Nodes.Count);
            foreach (string node in part.Nodes)
            {
                writer.Write(node);
            }
        }

        writer.Write(model.Animations.Count);
        foreach (AnimationClip clip in model.Animations)
        {
            writer.Write(clip.Name);
            writer.Write(clip.Duration);
            writer.Write(clip.JointCount);
            WriteArray(writer, clip.Frames);
        }
    }

    public static ModelData Read(Stream stream)
    {
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != Magic)
        {
            throw new InvalidDataException("Not a model file.");
        }

        int version = reader.ReadInt32();
        if (version != Version)
        {
            throw new InvalidDataException($"The model file is version {version}; this engine reads version {Version}. Build the content again.");
        }

        int partCount = reader.ReadInt32();
        List<MeshPart> parts = new(partCount);
        for (int index = 0; index < partCount; index++)
        {
            MeshVertex[] vertices = ReadArray<MeshVertex>(reader);
            uint[] indices = ReadArray<uint>(reader);
            SkinVertex[]? skin = null;
            if (reader.ReadBoolean())
            {
                skin = ReadArray<SkinVertex>(reader);
            }

            MaterialData material = ReadMaterial(reader);
            string[] nodes = new string[reader.ReadInt32()];
            for (int node = 0; node < nodes.Length; node++)
            {
                nodes[node] = reader.ReadString();
            }

            MeshPart part = new(vertices, indices, material) { Skin = skin, Nodes = nodes };
            parts.Add(part);
        }

        int animationCount = reader.ReadInt32();
        List<AnimationClip> animations = new(animationCount);
        for (int index = 0; index < animationCount; index++)
        {
            string name = reader.ReadString();
            float duration = reader.ReadSingle();
            int jointCount = reader.ReadInt32();
            Matrix4x4[] frames = ReadArray<Matrix4x4>(reader);
            AnimationClip clip = new(name, duration, jointCount, frames);
            animations.Add(clip);
        }

        return new ModelData(parts, animations);
    }

    private static void WriteMaterial(BinaryWriter writer, MaterialData material)
    {
        Vector4 color = material.BaseColor;
        writer.Write(color.X);
        writer.Write(color.Y);
        writer.Write(color.Z);
        writer.Write(color.W);
        writer.Write(material.BaseColorTexture is not null);
        if (material.BaseColorTexture is not null)
        {
            material.BaseColorTexture.Write(writer.BaseStream);
        }
    }

    private static MaterialData ReadMaterial(BinaryReader reader)
    {
        float red = reader.ReadSingle();
        float green = reader.ReadSingle();
        float blue = reader.ReadSingle();
        float alpha = reader.ReadSingle();
        DdsImage? texture = null;
        if (reader.ReadBoolean())
        {
            texture = DdsImage.Read(reader.BaseStream);
        }

        Vector4 color = new(red, green, blue, alpha);
        return new MaterialData(color, texture);
    }

    private static void WriteArray<T>(BinaryWriter writer, ReadOnlySpan<T> items)
        where T : unmanaged
    {
        writer.Write(items.Length);
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(items);
        writer.Write(bytes);
    }

    private static T[] ReadArray<T>(BinaryReader reader)
        where T : unmanaged
    {
        int length = reader.ReadInt32();
        T[] items = new T[length];
        Span<byte> bytes = MemoryMarshal.AsBytes(items.AsSpan());
        reader.BaseStream.ReadExactly(bytes);
        return items;
    }
}
