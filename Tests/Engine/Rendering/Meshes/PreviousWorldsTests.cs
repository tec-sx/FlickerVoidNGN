using System.Numerics;

using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Rendering.Tests.Meshes;

public sealed class PreviousWorldsTests
{
    private readonly PreviousWorldsTestsBuilder _builder = new();

    [Fact]
    public void GivesAMeshItsWorldFromTheFrameBefore()
    {
        PreviousWorlds worlds = _builder.Build();
        worlds.Begin();
        worlds.Remember(4, PreviousWorldsTestsBuilder.At(1));
        worlds.Begin();

        Matrix4x4 previous = worlds.Remember(4, PreviousWorldsTestsBuilder.At(2));

        previous.Should().Be(PreviousWorldsTestsBuilder.At(1));
    }

    [Fact]
    public void GivesAMeshNotDrawnBeforeItsOwnWorld()
    {
        PreviousWorlds worlds = _builder.Build();
        worlds.Begin();

        Matrix4x4 previous = worlds.Remember(4, PreviousWorldsTestsBuilder.At(1));

        previous.Should().Be(PreviousWorldsTestsBuilder.At(1));
    }

    [Fact]
    public void ForgetsAMeshNotDrawnInTheFrameBefore()
    {
        PreviousWorlds worlds = _builder.Build();
        worlds.Begin();
        worlds.Remember(4, PreviousWorldsTestsBuilder.At(1));
        worlds.Begin();
        worlds.Begin();

        Matrix4x4 previous = worlds.Remember(4, PreviousWorldsTestsBuilder.At(3));

        previous.Should().Be(PreviousWorldsTestsBuilder.At(3));
    }

    [Fact]
    public void KeepsEachMeshsWorldApart()
    {
        PreviousWorlds worlds = _builder.Build();
        worlds.Begin();
        worlds.Remember(1, PreviousWorldsTestsBuilder.At(1));
        worlds.Remember(2, PreviousWorldsTestsBuilder.At(2));
        worlds.Begin();

        Matrix4x4 first = worlds.Remember(1, PreviousWorldsTestsBuilder.At(5));
        Matrix4x4 second = worlds.Remember(2, PreviousWorldsTestsBuilder.At(6));

        first.Should().Be(PreviousWorldsTestsBuilder.At(1));
        second.Should().Be(PreviousWorldsTestsBuilder.At(2));
    }

    [Fact]
    public void GivesEveryPartOfAMeshTheSameWorldFromTheFrameBefore()
    {
        PreviousWorlds worlds = _builder.Build();
        worlds.Begin();
        worlds.Remember(4, PreviousWorldsTestsBuilder.At(1));
        worlds.Begin();
        worlds.Remember(4, PreviousWorldsTestsBuilder.At(2));

        Matrix4x4 again = worlds.Remember(4, PreviousWorldsTestsBuilder.At(2));

        again.Should().Be(PreviousWorldsTestsBuilder.At(1));
    }
}
