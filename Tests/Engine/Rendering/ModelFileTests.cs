using System.Numerics;

using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Rendering.Tests;

public sealed class ModelFileTests
{
    [Fact]
    public void ReadsWhatItWrites()
    {
        Vector4 orange = new(1, 0.5f, 0, 1);
        ModelData model = new TestModelBuilder().WithQuad(2).WithBaseColor(orange).WithRedBlueTexture().WithMovingJoint(Vector3.Zero, Vector3.UnitY, 1).Build();
        using MemoryStream stream = new();

        ModelFile.Write(model, stream);
        stream.Position = 0;
        ModelData read = ModelFile.Read(stream);

        MeshPart part = read.Parts.Should().ContainSingle().Subject;
        MeshPart written = model.Parts[0];
        part.Vertices.Should().Equal(written.Vertices);
        part.Indices.Should().Equal(written.Indices);
        part.Skin.Should().Equal(written.Skin);
        part.Material.BaseColor.Should().Be(orange);
        part.Material.BaseColorTexture!.Data.Should().Equal(written.Material.BaseColorTexture!.Data);
        AnimationClip clip = read.Animations.Should().ContainSingle().Subject;
        AnimationClip writtenClip = model.Animations[0];
        clip.Name.Should().Be(writtenClip.Name);
        clip.Duration.Should().Be(writtenClip.Duration);
        clip.JointCount.Should().Be(writtenClip.JointCount);
        clip.Frames.ToArray().Should().Equal(writtenClip.Frames.ToArray());
        stream.Position.Should().Be(stream.Length);
    }

    [Fact]
    public void ReadsARigidUntexturedModel()
    {
        ModelData model = new TestModelBuilder().WithQuad(1).Build();
        using MemoryStream stream = new();

        ModelFile.Write(model, stream);
        stream.Position = 0;
        ModelData read = ModelFile.Read(stream);

        read.Parts[0].Skin.Should().BeNull();
        read.Parts[0].Material.BaseColorTexture.Should().BeNull();
        read.Animations.Should().BeEmpty();
    }

    [Fact]
    public void ReadsTheNodesEachPartWasReadFrom()
    {
        ModelData built = new TestModelBuilder().WithQuad(1).Build();
        MeshPart named = built.Parts[0] with { Nodes = ["Handle.001", "door.1"] };
        ModelData model = new([named], []);
        using MemoryStream stream = new();

        ModelFile.Write(model, stream);
        stream.Position = 0;
        ModelData read = ModelFile.Read(stream);

        read.Parts[0].Nodes.Should().Equal("Handle.001", "door.1");
    }

    [Fact]
    public void AFileThatIsNotAModelThrows()
    {
        using MemoryStream stream = new([1, 2, 3, 4, 5, 6, 7, 8]);

        Action read = () => ModelFile.Read(stream);

        read.Should().Throw<InvalidDataException>();
    }
}
