using System.Numerics;

namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>A label the building creator shows over the viewport at a point in the world: a title, a fainter detail, and its kind.</summary>
public sealed record PlanLabel(Vector3 Point, string Title, string Detail, PlanLabelKind Kind)
{
    /// <summary>How far from its point's place on screen the label stands, in the view's units, so it sits beside a handle and not over it.</summary>
    public Vector2 Offset { get; init; }
}
