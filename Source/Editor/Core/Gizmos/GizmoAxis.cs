namespace FlickerVoid.Editor.Core.Gizmos;

/// <summary>
/// The handle of a gizmo under the mouse or being dragged; All is the uniform scale handle at the centre, and XY, YZ and XZ the
/// move handles that move on a plane, along both its axes.
/// </summary>
public enum GizmoAxis
{
    None,
    X,
    Y,
    Z,
    All,
    XY,
    YZ,
    XZ,
}
