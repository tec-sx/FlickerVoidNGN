namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>A number with both limits, which the view edits with a slider spanning them as well as a text box.</summary>
public sealed class RangedNumberFieldViewModel(IEditableValue property) : NumberFieldViewModel(property)
{
    public double SliderMinimum => Minimum!.Value;

    public double SliderMaximum => Maximum!.Value;
}
