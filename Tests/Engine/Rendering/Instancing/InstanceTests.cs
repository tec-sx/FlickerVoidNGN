using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Rendering.Instancing;

namespace FlickerVoid.Engine.Rendering.Tests.Instancing;

public sealed class InstanceTests
{
    [Fact]
    public void TakesSixteenBytes()
    {
        Marshal.SizeOf<Instance>().Should().Be(16);
    }

    [Fact]
    public void LaysOutItsPositionBeforeItsPackedWord()
    {
        Instance instance = new(new Vector3(1, 2, 3), turn: 0, scale: 1, variant: 0, windPhase: 0);
        Instance[] instances = [instance];

        float[] floats = MemoryMarshal.Cast<Instance, float>(instances).ToArray();
        uint[] words = MemoryMarshal.Cast<Instance, uint>(instances).ToArray();

        floats[..3].Should().Equal(1, 2, 3);
        words[3].Should().Be(0xFF00u, "the scale's byte is the second");
    }

    [Fact]
    public void KeepsWhereItStandsAndItsPackedValues()
    {
        Vector3 position = new(1, 2, 3);

        Instance instance = new(position, turn: MathF.PI / 2, scale: 1, variant: 7, windPhase: 200);

        instance.Position.Should().Be(position);
        instance.Turn.Should().BeApproximately(MathF.PI / 2, 1e-4f);
        instance.Scale.Should().Be(1);
        instance.Variant.Should().Be(7);
        instance.WindPhase.Should().Be(200);
    }

    [Theory]
    [InlineData(-MathF.PI / 2, 3 * MathF.PI / 2)]
    [InlineData(2 * MathF.PI, 0)]
    [InlineData(0.01f, 0)]
    public void RoundsTurnsToOneOf256StepsOfAWholeTurn(float turn, float expected)
    {
        Instance instance = new(Vector3.Zero, turn, scale: 0, variant: 0, windPhase: 0);

        instance.Turn.Should().BeApproximately(expected, 1e-4f);
    }

    [Theory]
    [InlineData(0.5f, 128f / 255)]
    [InlineData(-1f, 0f)]
    [InlineData(2f, 1f)]
    public void KeepsItsScaleAsAShareOfTheBatchsRange(float scale, float expected)
    {
        Instance instance = new(Vector3.Zero, turn: 0, scale, variant: 0, windPhase: 0);

        instance.Scale.Should().BeApproximately(expected, 1e-5f);
    }

    [Fact]
    public void PlacesItselfTurnedAndSizedAtItsPosition()
    {
        Vector3 position = new(4, 0, -2);
        Instance instance = new(position, turn: MathF.PI / 2, scale: 1, variant: 0, windPhase: 0);

        Matrix4x4 world = instance.ToMatrix(minScale: 0.5f, maxScale: 2);

        Vector3 placed = Vector3.Transform(Vector3.UnitX, world);
        placed.X.Should().BeApproximately(4, 1e-4f);
        placed.Z.Should().BeApproximately(-4, 1e-4f, "a quarter turn about the vertical takes +X to -Z, at twice the size");
    }
}
