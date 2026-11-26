using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Editor.Core.Inspector;

namespace FlickerVoid.Editor.Materials;

/// <summary>
/// One parameter row of the material editor: its field, whether the material overrides it (ticking copies the value shown in,
/// unticking takes it out, so the parent's or the default shows again), and where the value comes from while it doesn't.
/// </summary>
public sealed class MaterialParameterViewModel(MaterialParameterValue parameter, FieldViewModel field) : ObservableObject
{
    public FieldViewModel Field { get; } = field;

    public string Label => Field.Label;

    public bool IsOverridden
    {
        get => parameter.IsOverridden;
        set
        {
            if (value == IsOverridden)
            {
                return;
            }

            if (value)
            {
                parameter.Override();
            }
            else
            {
                parameter.Reset();
            }

            Refresh();
        }
    }

    /// <summary>The parent the value is kept from, or Default, shown while the material doesn't override it.</summary>
    public string Source => parameter.Source;

    /// <summary>Shows the material's value and whether it overrides it, after an edit.</summary>
    public void Refresh()
    {
        Field.Refresh();
        OnPropertyChanged(nameof(IsOverridden));
    }
}
