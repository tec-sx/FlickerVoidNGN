using FlickerVoid.Engine.Runtime.Debugging;

namespace FlickerVoid.Editor.Core.Settings;

/// <summary>A setting with named values, such as an enum's, shown as a list to pick from.</summary>
public sealed class ChoiceSettingViewModel(ConsoleVariable variable, Action saved) : SettingViewModel(variable, saved)
{
    public IReadOnlyList<string> Choices => Variable.Choices;

    public string Selected
    {
        get => Variable.Text;
        set => Set(value);
    }
}
