using System.Numerics;

using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>
/// Builds models as the content build bakes them: a quad facing +Z, wound clockwise, with a base color and an optional
/// texture. A moving joint skins every vertex fully to a second joint that an animation called Move slides.
/// </summary>
internal sealed class TestModelBuilder
{
    private const string AnimationName = "Move";

    private float _size = 1;
    private Vector4 _baseColor = Vector4.One;
    private DdsImage? _texture;
    private (Vector3 From, Vector3 To, float Seconds)? _movingJoint;

    /// <summary>Covers the square from -size to size in X and Y, with uvs from the top left.</summary>
    public TestModelBuilder WithQuad(float size)
    {
        _size = size;
        return this;
    }

    public TestModelBuilder WithBaseColor(Vector4 color)
    {
        _baseColor = color;
        return this;
    }

    /// <summary>A 2x1 texture, uncompressed so its texels stay exact: red, then blue.</summary>
    public TestModelBuilder WithRedBlueTexture()
    {
        byte[] texels = [255, 0, 0, 255, 0, 0, 255, 255];
        _texture = new DdsImage(2, 1, PixelFormat.Rgba8UNormSrgb, 1, texels);
        return this;
    }

    public TestModelBuilder WithMovingJoint(Vector3 from, Vector3 to, float seconds)
    {
        _movingJoint = (from, to, seconds);
        return this;
    }

    public ModelData Build()
    {
        MeshVertex[] vertices =
        [
            new(new Vector3(-_size, -_size, 0), Vector3.UnitZ, new Vector2(0, 1)),
            new(new Vector3(_size, -_size, 0), Vector3.UnitZ, new Vector2(1, 1)),
            new(new Vector3(_size, _size, 0), Vector3.UnitZ, new Vector2(1, 0)),
            new(new Vector3(-_size, _size, 0), Vector3.UnitZ, new Vector2(0, 0)),
        ];
        uint[] indices = [0, 2, 1, 0, 3, 2];
        MaterialData material = new(_baseColor, _texture);
        MeshPart part = new(vertices, indices, material);
        if (_movingJoint is not { } joint)
        {
            return new ModelData([part], []);
        }

        SkinVertex followsSecondJoint = new(1, 0, 0, 0, new Vector4(1, 0, 0, 0));
        SkinVertex[] skin = [.. Enumerable.Repeat(followsSecondJoint, vertices.Length)];
        MeshPart skinned = part with { Skin = skin };
        AnimationClip clip = BakeMove(joint.From, joint.To, joint.Seconds);
        return new ModelData([skinned], [clip]);
    }

    private static AnimationClip BakeMove(Vector3 from, Vector3 to, float seconds)
    {
        const int jointCount = 2;
        int frameCount = (int)MathF.Ceiling(seconds * AnimationClip.FrameRate) + 1;
        Matrix4x4[] frames = new Matrix4x4[frameCount * jointCount];
        for (int frame = 0; frame < frameCount; frame++)
        {
            float time = MathF.Min(frame / AnimationClip.FrameRate, seconds);
            Vector3 offset = Vector3.Lerp(from, to, time / seconds);
            frames[frame * jointCount] = Matrix4x4.Identity;
            frames[(frame * jointCount) + 1] = Matrix4x4.CreateTranslation(offset);
        }

        return new AnimationClip(AnimationName, seconds, jointCount, frames);
    }
}
