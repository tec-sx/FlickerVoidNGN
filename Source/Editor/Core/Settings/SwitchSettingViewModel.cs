using FlickerVoid.Engine.Runtime.Debugging;

namespace FlickerVoid.Editor.Core.Settings;

/// <summary>A boolean setting, shown as a check box.</summary>
public sealed class SwitchSettingViewModel(ConsoleVariable variable, Action saved) : SettingViewModel(variable, saved)
{
    public bool IsOn
    {
        get => Variable.Text == "true";
        set => Set(value ? "true" : "false");
    }
}
