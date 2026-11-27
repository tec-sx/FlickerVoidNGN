using FlickerVoid.Editor.Core.Buildings;

namespace FlickerVoid.Editor.Buildings;

/// <summary>A plan label placed over the viewport: where it stands in the view's own units, what it says and how it looks.</summary>
public sealed record PlanLabelView(double X, double Y, string Title, string Detail, PlanLabelKind Kind)
{
    /// <summary>The size of the box each label is centred in, so it stands centred on its point.</summary>
    public const double BoxWidth = 320;

    public const double BoxHeight = 80;

    public double Left => X - (BoxWidth / 2);

    public double Top => Y - (BoxHeight / 2);

    public bool IsRoom => Kind == PlanLabelKind.Room;

    public bool IsTag => Kind != PlanLabelKind.Room;

    public bool IsDrag => Kind == PlanLabelKind.Drag;

    public bool HasDetail => Detail.Length > 0;
}
