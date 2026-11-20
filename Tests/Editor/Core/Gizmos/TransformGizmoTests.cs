using System.Numerics;

using FlickerVoid.Editor.Core.Gizmos;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Tests.Gizmos;

public sealed class TransformGizmoTests
{
    private const int Width = TransformGizmoTestsBuilder.Width;
    private const int Height = TransformGizmoTestsBuilder.Height;
    private const float Precision = 0.02f;

    private readonly TransformGizmoTestsBuilder _builder = new();

    [Theory]
    [InlineData(GizmoAxis.X, 1, 0, 0)]
    [InlineData(GizmoAxis.Y, 0, 1, 0)]
    [InlineData(GizmoAxis.Z, 0, 0, 1)]
    public void HoversTheMoveHandleUnderTheMouse(GizmoAxis axis, float x, float y, float z)
    {
        TransformGizmo gizmo = _builder.Build();
        Vector2 pixel = _builder.PixelAt(gizmo, new Vector3(x, y, z) * 0.7f);

        GizmoAxis hovered = gizmo.Hover(_builder.Camera, _builder.Target, pixel, Width, Height);

        hovered.Should().Be(axis);
    }

    [Fact]
    public void HoversNothingAwayFromTheHandles()
    {
        TransformGizmo gizmo = _builder.Build();
        Vector2 pixel = _builder.PixelAt(gizmo, new Vector3(3, 3, 0));

        GizmoAxis hovered = gizmo.Hover(_builder.Camera, _builder.Target, pixel, Width, Height);

        hovered.Should().Be(GizmoAxis.None);
    }

    [Fact]
    public void HasNoHandlesInSelectMode()
    {
        TransformGizmo gizmo = _builder.InMode(GizmoMode.Select).Build();
        Vector2 pixel = _builder.PixelAt(gizmo, new Vector3(0.7f, 0, 0));

        bool dragging = gizmo.BeginDrag(_builder.Camera, _builder.Target, pixel, Width, Height);

        dragging.Should().BeFalse();
    }

    [Fact]
    public void MovesAlongTheDraggedAxis()
    {
        TransformGizmo gizmo = _builder.Build();
        Vector2 start = _builder.PixelAt(gizmo, new Vector3(0.7f, 0, 0));
        gizmo.BeginDrag(_builder.Camera, _builder.Target, start, Width, Height);
        float length = gizmo.GetHandleLength(_builder.Camera, Vector3.Zero, Height);
        Vector3 end = new((0.7f * length) + 1, 0, 0);
        Vector2 endPixel = _builder.Camera.Project(end, Width, Height);

        Transform moved = gizmo.Drag(_builder.Camera, endPixel, Width, Height);

        Vector3.Distance(moved.Position, new Vector3(1, 0, 0)).Should().BeLessThan(Precision);
    }

    [Fact]
    public void RotatesRoundTheDraggedRing()
    {
        TransformGizmo gizmo = _builder.InMode(GizmoMode.Rotate).LookingDown().Build();
        float diagonal = MathF.Sqrt(0.5f);
        Vector2 start = _builder.PixelAt(gizmo, new Vector3(diagonal, 0, -diagonal));
        Vector2 end = _builder.PixelAt(gizmo, new Vector3(-diagonal, 0, -diagonal));

        bool dragging = gizmo.BeginDrag(_builder.Camera, _builder.Target, start, Width, Height);
        Transform rotated = gizmo.Drag(_builder.Camera, end, Width, Height);

        dragging.Should().BeTrue();
        gizmo.Hovered.Should().Be(GizmoAxis.Y);
        Vector3 turnedX = Vector3.Transform(Vector3.UnitX, rotated.Rotation);
        Vector3.Distance(turnedX, -Vector3.UnitZ).Should().BeLessThan(Precision, "a quarter turn round +Y carries +X to -Z");
    }

    [Fact]
    public void ScalesAlongTheDraggedAxis()
    {
        TransformGizmo gizmo = _builder.InMode(GizmoMode.Scale).Build();
        Vector2 start = _builder.PixelAt(gizmo, new Vector3(0.5f, 0, 0));
        Vector2 end = _builder.PixelAt(gizmo, new Vector3(1, 0, 0));

        gizmo.BeginDrag(_builder.Camera, _builder.Target, start, Width, Height);
        Transform scaled = gizmo.Drag(_builder.Camera, end, Width, Height);

        Vector3.Distance(scaled.Scale, new Vector3(1.5f, 1, 1)).Should().BeLessThan(Precision);
    }

    [Fact]
    public void ScalesEvenlyFromItsCentre()
    {
        TransformGizmo gizmo = _builder.InMode(GizmoMode.Scale).Build();
        Vector2 centre = _builder.PixelAt(gizmo, Vector3.Zero);
        Vector2 end = centre + new Vector2(TransformGizmo.PixelsPerUniformScale, 0);

        gizmo.BeginDrag(_builder.Camera, _builder.Target, centre, Width, Height);
        Transform scaled = gizmo.Drag(_builder.Camera, end, Width, Height);

        gizmo.Hovered.Should().Be(GizmoAxis.All);
        Vector3.Distance(scaled.Scale, new Vector3(2)).Should().BeLessThan(Precision);
    }

    [Fact]
    public void KeepsTheTransformItStartedFromWhenTheMouseComesBack()
    {
        TransformGizmo gizmo = _builder.Build();
        Vector2 start = _builder.PixelAt(gizmo, new Vector3(0, 0.7f, 0));
        gizmo.BeginDrag(_builder.Camera, _builder.Target, start, Width, Height);
        Vector2 away = start + new Vector2(0, -60);
        gizmo.Drag(_builder.Camera, away, Width, Height);

        Transform back = gizmo.Drag(_builder.Camera, start, Width, Height);

        Vector3.Distance(back.Position, Vector3.Zero).Should().BeLessThan(Precision);
    }

    [Fact]
    public void SnapsTheDraggedAxisToTheGridWhileMoveSnappingIsOn()
    {
        _builder.Target = new Transform { Position = new Vector3(0.3f, 0.3f, 0) };
        TransformGizmo gizmo = _builder.Build();
        gizmo.MoveSnap.IsOn = true;
        gizmo.MoveSnap.Step = 0.5f;
        Vector2 start = _builder.PixelAt(gizmo, new Vector3(0.7f, 0, 0));
        gizmo.BeginDrag(_builder.Camera, _builder.Target, start, Width, Height);
        float length = gizmo.GetHandleLength(_builder.Camera, _builder.Target.Position, Height);
        Vector3 end = new(0.3f + (0.7f * length) + 1.1f, 0.3f, 0);
        Vector2 endPixel = _builder.Camera.Project(end, Width, Height);

        Transform moved = gizmo.Drag(_builder.Camera, endPixel, Width, Height);

        moved.Position.X.Should().Be(1.5f);
        moved.Position.Y.Should().Be(0.3f);
    }

    [Fact]
    public void TurnsInStepsWhileRotateSnappingIsOn()
    {
        TransformGizmo gizmo = _builder.InMode(GizmoMode.Rotate).LookingDown().Build();
        gizmo.RotateSnap.IsOn = true;
        gizmo.RotateSnap.Step = 45;
        float diagonal = MathF.Sqrt(0.5f);
        Vector3 from = new(diagonal, 0, -diagonal);
        Quaternion eightyDegrees = Quaternion.CreateFromAxisAngle(Vector3.UnitY, float.DegreesToRadians(80));
        Vector3 to = Vector3.Transform(from, eightyDegrees);
        Vector2 start = _builder.PixelAt(gizmo, from);
        Vector2 end = _builder.PixelAt(gizmo, to);

        gizmo.BeginDrag(_builder.Camera, _builder.Target, start, Width, Height);
        Transform rotated = gizmo.Drag(_builder.Camera, end, Width, Height);

        Vector3 turnedX = Vector3.Transform(Vector3.UnitX, rotated.Rotation);
        Vector3.Distance(turnedX, -Vector3.UnitZ).Should().BeLessThan(0.0001f, "80 degrees snaps to a quarter turn");
    }

    [Fact]
    public void ScalesInStepsOfTheStartingScaleWhileScaleSnappingIsOn()
    {
        _builder.Target = new Transform { Scale = new Vector3(2, 1, 1) };
        TransformGizmo gizmo = _builder.InMode(GizmoMode.Scale).Build();
        gizmo.ScaleSnap.IsOn = true;
        gizmo.ScaleSnap.Step = 0.25f;
        Vector2 start = _builder.PixelAt(gizmo, new Vector3(0.5f, 0, 0));
        Vector2 end = _builder.PixelAt(gizmo, new Vector3(1.05f, 0, 0));

        gizmo.BeginDrag(_builder.Camera, _builder.Target, start, Width, Height);
        Transform scaled = gizmo.Drag(_builder.Camera, end, Width, Height);

        scaled.Scale.Should().Be(new Vector3(3, 1, 1), "a drag of 1.55 times snaps to 1.5 times the starting scale");
    }
}
