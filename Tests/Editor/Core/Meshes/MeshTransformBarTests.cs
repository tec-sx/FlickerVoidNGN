using System.Numerics;

using FlickerVoid.Editor.Core.Creators;
using FlickerVoid.Editor.Core.Meshes;
using FlickerVoid.Editor.Core.Tests.Creators;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class MeshTransformBarTests : IDisposable
{
    private readonly CreatorModeTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void EachToolStartsFromItsNeutralValues()
    {
        MeshTransformBar bar = new() { Tool = MeshTransformTool.Scale };

        bar.Values.Should().Be(Vector3.One);
        bar.Tool = MeshTransformTool.Rotate;
        bar.Values.Should().Be(Vector3.Zero);
    }

    [Fact]
    public void AppliesTheTypedOffsetToTheSelectionAndGoesBackToNeutral()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        mode.MeshSelection!.Select(MeshHit.OfPoint(0), additive: false);
        float before = _builder.FindMesh("body")!.Points[0].X;
        MeshTransformBar bar = new() { Tool = MeshTransformTool.Move, Values = new Vector3(0.5f, 0, 0) };

        bool done = bar.Apply(mode);

        done.Should().BeTrue();
        _builder.FindMesh("body")!.Points[0].X.Should().BeApproximately(before + 0.5f, 1e-5f);
        bar.Values.Should().Be(Vector3.Zero);
    }

    [Fact]
    public void WithNothingSelectedItKeepsTheTypedValues()
    {
        using CreatorMode mode = _builder.EnterAlone();
        mode.SelectPart("body");
        mode.EnterMesh();
        MeshTransformBar bar = new() { Tool = MeshTransformTool.Rotate, Values = new Vector3(0, 90, 0) };

        bool done = bar.Apply(mode);

        done.Should().BeFalse();
        bar.Values.Should().Be(new Vector3(0, 90, 0));
    }
}
