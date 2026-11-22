namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>A string naming a content file, such as a mesh, chosen from the files it may name with an <see cref="AssetPickerViewModel"/>.</summary>
public sealed class AssetFieldViewModel : FieldViewModel
{
    public AssetFieldViewModel(IEditableValue property, Func<IReadOnlyList<string>> find)
        : base(property)
    {
        Picker = new AssetPickerViewModel(find, property.AcceptsNull, Write);
    }

    public AssetPickerViewModel Picker { get; }

    protected override void Show(object? value)
    {
        Picker.Current = value as string;
    }
}
