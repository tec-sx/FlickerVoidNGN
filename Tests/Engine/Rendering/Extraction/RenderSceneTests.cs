using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Tests.Extraction;

public sealed class RenderSceneTests
{
    private readonly RenderSceneTestsBuilder _builder = new();

    [Fact]
    public void ACopyHoldsEverythingTheSceneHolds()
    {
        RenderScene source = _builder.BuildFull();
        RenderScene copy = new();

        copy.CopyFrom(source);

        copy.Should().BeEquivalentTo(source);
    }

    [Fact]
    public void ACopyKeepsTheIndoorVolumesAndTheCutaway()
    {
        RenderScene source = _builder.BuildFull();
        RenderScene copy = new();

        copy.CopyFrom(source);

        copy.Indoors.Should().Equal(source.Indoors);
        copy.Cutaway.Should().Be(source.Cutaway);
    }

    [Fact]
    public void ACopyReplacesWhatTheSceneHeldBefore()
    {
        RenderScene copy = _builder.BuildFull();
        RenderScene empty = new();

        copy.CopyFrom(empty);

        copy.Should().BeEquivalentTo(empty);
    }
}
