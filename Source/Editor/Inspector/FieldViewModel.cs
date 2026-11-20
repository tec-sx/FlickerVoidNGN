using CommunityToolkit.Mvvm.ComponentModel;

namespace FlickerVoid.Engine.Editor.Inspector;

/// <summary>Edits one value, such as a property of a component; the inspector's view picks the control by the field's type.</summary>
public abstract class FieldViewModel(IEditableValue property) : ObservableObject
{
    private bool _refreshing;

    public string Label => Property.Label;

    public bool IsReadOnly => !Property.CanWrite;

    protected IEditableValue Property { get; } = property;

    /// <summary>Shows the property's value in the world, without writing it back.</summary>
    public void Refresh()
    {
        _refreshing = true;
        try
        {
            object? value = Property.Get();
            Show(value);
        }
        finally
        {
            _refreshing = false;
        }
    }

    protected abstract void Show(object? value);

    /// <summary>The float as written, 1.6 rather than the 1.600000023841858 a plain conversion to double shows.</summary>
    protected static double ToShown(float value)
    {
        decimal rounded = (decimal)value;
        return (double)rounded;
    }

    /// <summary>Writes a value the user entered; does nothing while the field is showing the world's value.</summary>
    protected void Write(object? value)
    {
        if (_refreshing || IsReadOnly)
        {
            return;
        }

        Property.Set(value);
    }
}
