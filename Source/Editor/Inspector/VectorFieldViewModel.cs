using System.Numerics;

using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>A <see cref="Vector3"/> property, such as a position or a size, edited as X, Y and Z.</summary>
public sealed partial class VectorFieldViewModel(IEditableValue property) : FieldViewModel(property)
{
    [ObservableProperty]
    public partial double X { get; set; }

    [ObservableProperty]
    public partial double Y { get; set; }

    [ObservableProperty]
    public partial double Z { get; set; }

    protected override void Show(object? value)
    {
        Vector3 vector = (Vector3)value!;
        X = ToShown(vector.X);
        Y = ToShown(vector.Y);
        Z = ToShown(vector.Z);
    }

    partial void OnXChanged(double value) => WriteVector();

    partial void OnYChanged(double value) => WriteVector();

    partial void OnZChanged(double value) => WriteVector();

    private void WriteVector()
    {
        Vector3 vector = new((float)X, (float)Y, (float)Z);
        Write(vector);
    }
}
