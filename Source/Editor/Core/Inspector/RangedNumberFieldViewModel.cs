namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>A number with both limits, which the view drags across them in about two hundred steps as well as taking typed values.</summary>
public sealed class RangedNumberFieldViewModel(IEditableValue property) : NumberFieldViewModel(property)
{
    private const double DragSteps = 200;

    public double Lowest => Minimum!.Value;

    public double Highest => Maximum!.Value;

    public double DragStep => (Highest - Lowest) / DragSteps;
}
