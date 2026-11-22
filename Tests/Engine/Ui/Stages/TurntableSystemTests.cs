using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Ui.Stages;

namespace FlickerVoid.Engine.Ui.Tests.Stages;

public sealed class TurntableSystemTests : IDisposable
{
    private readonly TurntableSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void SpinsAtItsSpeedAboutTheUpAxis()
    {
        Entity entity = _builder.WithTurntable(new Turntable { Speed = 90 }).Build();

        TurntableSystem.Spin(_builder.World, 0.5f);

        _builder.Yaw(entity).Should().BeApproximately(45, 0.01f);
    }

    [Fact]
    public void DragsByItsDegreesPerPixel()
    {
        Entity entity = _builder.WithTurntable(new Turntable { Speed = 0, DragSpeed = 0.5f }).Build();

        TurntableSystem.Drag(_builder.World, 40);

        _builder.Yaw(entity).Should().BeApproximately(20, 0.01f);
    }

    [Fact]
    public void KeepsItsTiltWhileTurning()
    {
        Quaternion tilt = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.3f);
        Entity entity = _builder.WithTurntable(new Turntable { Speed = 180 }).WithRotation(tilt).Build();

        TurntableSystem.Spin(_builder.World, 1);

        Transform transform = _builder.World.Get<Transform>(entity);
        Vector3 up = Vector3.Transform(Vector3.UnitY, transform.Rotation);
        Vector3 tiltedUp = Vector3.Transform(Vector3.UnitY, tilt);
        up.Y.Should().BeApproximately(tiltedUp.Y, 0.001f);
    }

    [Fact]
    public void SpinsTheGamesTurntablesEachFrame()
    {
        Entity entity = _builder.WithTurntable(new Turntable { Speed = 60 }).Build();
        TurntableSystem system = new(_builder.World);

        system.Update(new Runtime.Loop.GameTime(TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1), 1));

        _builder.Yaw(entity).Should().BeApproximately(30, 0.01f);
    }
}
