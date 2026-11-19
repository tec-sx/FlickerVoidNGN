using System.Numerics;

using FlickerVoid.Engine.Editor.Cameras;
using FlickerVoid.Engine.Editor.Gizmos;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Editor.Tests.Gizmos;

/// <summary>
/// Builds a gizmo in a mode over a transform at the origin, seen by a camera in a 640x480 viewport; by default the camera looks
/// at the origin from above at an angle, so all three axes show.
/// </summary>
internal sealed class TransformGizmoTestsBuilder
{
    public const int Width = 640;
    public const int Height = 480;

    private GizmoMode _mode = GizmoMode.Move;

    public TransformGizmoTestsBuilder()
    {
        Camera.LookAt(Vector3.Zero, 0.6f, -0.5f, 10);
    }

    public EditorCamera Camera { get; } = new();

    public Transform Target { get; set; } = new();

    public TransformGizmoTestsBuilder InMode(GizmoMode mode)
    {
        _mode = mode;
        return this;
    }

    public TransformGizmoTestsBuilder LookingDown()
    {
        Camera.LookAt(Vector3.Zero, 0, -EditorCamera.PitchLimit, 10);
        return this;
    }

    public TransformGizmo Build()
    {
        return new TransformGizmo { Mode = _mode };
    }

    /// <summary>The pixel a point on the gizmo shows at, given as a direction from its centre in multiples of the handle length.</summary>
    public Vector2 PixelAt(TransformGizmo gizmo, Vector3 handleLengths)
    {
        float length = gizmo.GetHandleLength(Camera, Target.Position, Height);
        Vector3 point = Target.Position + (handleLengths * length);
        return Camera.Project(point, Width, Height);
    }
}
