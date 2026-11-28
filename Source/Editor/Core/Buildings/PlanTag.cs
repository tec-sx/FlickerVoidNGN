using System.Numerics;

namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>A plan label's tag over the viewport: where it points, how big it shows, and its kind.</summary>
public readonly record struct PlanTag(Vector2 Centre, Vector2 Size, PlanLabelKind Kind);
