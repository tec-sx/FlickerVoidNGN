using System.Numerics;

using BCnEncoder.Decoder;
using BCnEncoder.Shared;

using FlickerVoid.Engine.Builder.Models;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Builder.Tests.Models;

public sealed class GltfImporterTests
{
    private readonly GltfImporterTestsBuilder _builder = new();

    [Fact]
    public void ImportsEachVertexWithItsNormalAndUv()
    {
        IReadOnlyList<MeshPart> parts = _builder.WithTriangle().Build();

        MeshVertex[] corners = GltfImporterTestsBuilder.Corners(parts[0]);
        corners.Select(static vertex => vertex.Normal).Should().AllBeEquivalentTo(Vector3.UnitZ);
        corners.Select(static vertex => vertex.Uv).Should().Contain([Vector2.Zero, Vector2.UnitX, Vector2.UnitY]);
    }

    [Fact]
    public void WindsTrianglesClockwise()
    {
        IReadOnlyList<MeshPart> parts = _builder.WithTriangle().Build();

        MeshVertex[] corners = GltfImporterTestsBuilder.Corners(parts[0]);
        Vector3 edge1 = corners[1].Position - corners[0].Position;
        Vector3 edge2 = corners[2].Position - corners[0].Position;
        Vector3 facing = Vector3.Cross(edge1, edge2);
        facing.Z.Should().BeNegative("the counter-clockwise glTF triangle faces +Z, so clockwise order turns its cross product to -Z");
    }

    [Fact]
    public void BakesTheNodeTransformIntoTheVertices()
    {
        Matrix4x4 turnAndMove = Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(1, 2, 3);
        IReadOnlyList<MeshPart> parts = _builder.WithTriangle().WithNodeTransform(turnAndMove).Build();

        MeshVertex[] corners = GltfImporterTestsBuilder.Corners(parts[0]);
        corners.Select(static vertex => vertex.Position).Should().Contain(new Vector3(1, 2, 3));
        corners[0].Normal.X.Should().BeApproximately(1, 1e-5f);
    }

    [Fact]
    public void KeepsWindingClockwiseUnderAMirroringTransform()
    {
        Matrix4x4 mirror = Matrix4x4.CreateScale(1, 1, -1);
        IReadOnlyList<MeshPart> parts = _builder.WithTriangle().WithNodeTransform(mirror).Build();

        MeshVertex[] corners = GltfImporterTestsBuilder.Corners(parts[0]);
        Vector3 edge1 = corners[1].Position - corners[0].Position;
        Vector3 edge2 = corners[2].Position - corners[0].Position;
        Vector3 facing = Vector3.Cross(edge1, edge2);
        Vector3 normal = corners[0].Normal;
        Vector3.Dot(facing, normal).Should().BeNegative("clockwise triangles face away from their cross product");
    }

    [Fact]
    public void ImportsTheBaseColorFactor()
    {
        Vector4 orange = new(1, 0.5f, 0, 1);
        IReadOnlyList<MeshPart> parts = _builder.WithTriangle().WithBaseColor(orange).Build();

        parts[0].Material.BaseColor.Should().Be(orange);
        parts[0].Material.BaseColorTexture.Should().BeNull();
    }

    [Fact]
    public void CompressesTheBaseColorTextureToSrgbBc7()
    {
        IReadOnlyList<MeshPart> parts = _builder.WithTriangle().WithRedBlueTexture().Build();

        DdsImage? texture = parts[0].Material.BaseColorTexture;
        texture.Should().NotBeNull();
        texture.Width.Should().Be(2);
        texture.Height.Should().Be(1);
        texture.Format.Should().Be(PixelFormat.Bc7UNormSrgb);
        texture.MipLevels.Should().Be(2);
        byte[] topLevel = texture.Data[..16];
        BcDecoder decoder = new();
        ColorRgba32[] texels = decoder.DecodeRaw(topLevel, 2, 1, CompressionFormat.Bc7);
        texels[0].r.Should().BeInRange(250, 255);
        texels[0].b.Should().BeInRange(0, 5);
        texels[1].r.Should().BeInRange(0, 5);
        texels[1].b.Should().BeInRange(250, 255);
    }

    [Fact]
    public void LeavesRigidModelsUnskinned()
    {
        ModelData data = _builder.WithTriangle().BuildData();

        data.Parts[0].Skin.Should().BeNull();
        data.Animations.Should().BeEmpty();
    }

    [Fact]
    public void ImportsTheJointsAndWeightsOfSkinnedVertices()
    {
        ModelData data = _builder.WithTriangle().WithMovingJoint(Vector3.Zero, Vector3.UnitX, 1).BuildData();

        SkinVertex[] skin = data.Parts[0].Skin!;
        skin.Should().HaveCount(data.Parts[0].Vertices.Length);
        skin[0].Joint0.Should().Be(1);
        skin[0].Weights.X.Should().Be(1);
    }

    [Fact]
    public void BakesEachAnimationIntoSkinMatricesForEveryJoint()
    {
        ModelData data = _builder.WithTriangle().WithMovingJoint(Vector3.Zero, new Vector3(2, 0, 0), 1).BuildData();
        AnimationClip clip = data.Animations.Single();
        Matrix4x4[] skin = new Matrix4x4[clip.JointCount];

        clip.Sample(0.5f, skin);

        clip.Name.Should().Be("Move");
        clip.Duration.Should().BeApproximately(1, 1e-5f);
        clip.JointCount.Should().Be(2);
        skin[1].Translation.X.Should().BeApproximately(1, 1e-4f);
        skin[0].Should().Be(Matrix4x4.Identity);
    }

    [Fact]
    public void LoopsAnimationsPastTheirEnd()
    {
        ModelData data = _builder.WithTriangle().WithMovingJoint(Vector3.Zero, new Vector3(2, 0, 0), 1).BuildData();
        AnimationClip clip = data.Animations.Single();
        Matrix4x4[] early = new Matrix4x4[clip.JointCount];
        Matrix4x4[] looped = new Matrix4x4[clip.JointCount];

        clip.Sample(0.25f, early);
        clip.Sample(1.25f, looped);

        looped[1].Translation.X.Should().BeApproximately(early[1].Translation.X, 1e-4f);
    }
}
