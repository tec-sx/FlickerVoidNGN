using FlickerVoid.Engine.Runtime.Debugging;

namespace FlickerVoid.Editor.Core.Settings;

/// <summary>A number or text setting, shown as a box to type in; text that is not a value of the variable is ignored.</summary>
public sealed class TextSettingViewModel(ConsoleVariable variable, Action saved) : SettingViewModel(variable, saved)
{
    public string Text
    {
        get => Variable.Text;
        set => Set(value);
    }
}
