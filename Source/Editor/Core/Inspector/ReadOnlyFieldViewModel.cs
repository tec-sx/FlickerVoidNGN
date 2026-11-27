using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>A property the inspector has no editor for, shown as it would be written in a scene file; a shaped solid by its counts.</summary>
public sealed partial class ReadOnlyFieldViewModel(IEditableValue property) : FieldViewModel(property)
{
    [ObservableProperty]
    public partial string Text { get; set; } = "";

    protected override void Show(object? value)
    {
        if (value is SolidMesh solid)
        {
            Text = $"{solid.Points.Count} points, {solid.Faces.Count} faces";
            return;
        }

        Text = Property.ToJson(value);
    }
}
