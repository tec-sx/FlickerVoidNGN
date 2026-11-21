using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Gameplay.Protagonist.Control;

using Silk.NET.Input;

namespace FlickerVoid.Gameplay.Tests.Protagonist.Control;

public sealed class WalkControlSystemTests : IDisposable
{
    private readonly WalkControlSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void WWalksTheFoxAwayFromTheCamera()
    {
        _builder.Build();
        IReadOnlyList<Entity> entities = _builder.Spawn(WalkControlSystemTestsBuilder.Camera + "," + WalkControlSystemTestsBuilder.Fox);
        _builder.Hold(Key.W);

        _builder.Run(1);

        Vector3 position = _builder.Game.World.Get<Transform>(entities[1]).Position;
        position.Z.Should().BeApproximately(-2, 0.15f);
        position.X.Should().BeApproximately(0, 1e-3f);
    }

    [Fact]
    public void DWalksTheFoxToTheScreensRightAndReleasingStopsIt()
    {
        _builder.Build();
        IReadOnlyList<Entity> entities = _builder.Spawn(WalkControlSystemTestsBuilder.Camera + "," + WalkControlSystemTestsBuilder.Fox);
        _builder.Hold(Key.D);
        _builder.Run(0.5f);

        _builder.Release(Key.D);
        _builder.Run(0.1f);
        Vector3 stopped = _builder.Game.World.Get<Transform>(entities[1]).Position;
        _builder.Run(0.5f);

        stopped.X.Should().BeGreaterThan(0.8f);
        _builder.Game.World.Get<Transform>(entities[1]).Position.Should().Be(stopped);
    }

    [Fact]
    public void TwoKeysWalkDiagonallyAtTheFoxsSpeed()
    {
        _builder.Build();
        IReadOnlyList<Entity> entities = _builder.Spawn(WalkControlSystemTestsBuilder.Camera + "," + WalkControlSystemTestsBuilder.Fox);
        _builder.Hold(Key.W);
        _builder.Hold(Key.D);

        _builder.Run(1);

        Vector3 position = _builder.Game.World.Get<Transform>(entities[1]).Position;
        position.Length().Should().BeApproximately(2, 0.15f);
        position.X.Should().BeApproximately(-position.Z, 0.05f);
    }

    [Theory]
    [InlineData(-90)]
    [InlineData(-30)]
    [InlineData(0)]
    public void UpOnTheScreenIsAwayFromTheCameraAtAnyTilt(float pitch)
    {
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, float.DegreesToRadians(pitch));

        Vector3 direction = WalkControlSystem.ToGround(new Vector2(0, 1), rotation);

        direction.Z.Should().BeApproximately(-1, 1e-4f);
        direction.X.Should().BeApproximately(0, 1e-4f);
    }
}
