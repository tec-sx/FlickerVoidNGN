using FlickerVoid.Editor.Core.Buildings;

namespace FlickerVoid.Editor.Buildings;

/// <summary>A plan label placed over the viewport: where it stands in the view's own units, what it says and how it looks.</summary>
public sealed record PlanLabelView(double X, double Y, string Title, string Detail, PlanLabelKind Kind)
{
    public bool IsRoom => Kind == PlanLabelKind.Room;

    public bool IsTag => Kind != PlanLabelKind.Room;

    public bool IsDrag => Kind == PlanLabelKind.Drag;

    public bool HasDetail => Detail.Length > 0;
}
