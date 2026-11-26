using System.Numerics;

using FlickerVoid.Editor.Core.Meshes.Booleans;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Tests.Meshes;

public sealed class SolidBooleanTests
{
    private readonly SolidMesh _box = SolidMesh.Box(Vector3.One);

    [Fact]
    public void ABoxCutThroughByANarrowerOneLosesItsVolume()
    {
        SolidBooleanTestsBuilder.RequireLibrary();
        SolidMesh drill = SolidBooleanTestsBuilder.BoxAt(new Vector3(0.4f, 3, 0.4f), new Vector3(0, -1, 0));

        SolidMesh? result = SolidBoolean.Combine(_box, drill, BooleanOperation.Subtract);

        result.Should().NotBeNull();
        SolidBooleanTestsBuilder.GetVolume(result!).Should().BeApproximately(1 - (0.4f * 0.4f), 1e-3f);
    }

    [Fact]
    public void TwoOverlappingBoxesAddUpToTheirUnion()
    {
        SolidBooleanTestsBuilder.RequireLibrary();
        SolidMesh other = SolidBooleanTestsBuilder.BoxAt(Vector3.One, new Vector3(0.5f, 0, 0));

        SolidMesh? result = SolidBoolean.Combine(_box, other, BooleanOperation.Add);

        SolidBooleanTestsBuilder.GetVolume(result!).Should().BeApproximately(1.5f, 1e-3f);
    }

    [Fact]
    public void WhatTwoBoxesShareIsABoxOfSixQuads()
    {
        SolidBooleanTestsBuilder.RequireLibrary();
        SolidMesh other = SolidBooleanTestsBuilder.BoxAt(Vector3.One, new Vector3(0.5f, 0, 0));

        SolidMesh? result = SolidBoolean.Combine(_box, other, BooleanOperation.Intersect);

        SolidBooleanTestsBuilder.GetVolume(result!).Should().BeApproximately(0.5f, 1e-3f);
        result!.Faces.Should().HaveCount(6);
        result.Faces.Should().AllSatisfy(face => face.Count.Should().Be(4));
    }

    [Fact]
    public void NothingLeftGivesNothing()
    {
        SolidBooleanTestsBuilder.RequireLibrary();
        SolidMesh bigger = SolidMesh.Box(new Vector3(3, 3, 3));
        SolidMesh inside = SolidBooleanTestsBuilder.BoxAt(Vector3.One, new Vector3(0, 0.5f, 0));

        SolidMesh? result = SolidBoolean.Combine(inside, bigger, BooleanOperation.Subtract);

        result.Should().BeNull();
    }
}
