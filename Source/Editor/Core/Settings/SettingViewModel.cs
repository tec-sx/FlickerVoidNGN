using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Runtime.Debugging;

namespace FlickerVoid.Editor.Core.Settings;

/// <summary>One saved console variable on a project settings page, named for people, which saves the settings file when it is set.</summary>
public abstract class SettingViewModel(ConsoleVariable variable, Action saved) : ObservableObject
{
    public ConsoleVariable Variable { get; } = variable;

    /// <summary>The variable's name without its prefix, its words apart: "r.RayTracing" is "Ray tracing".</summary>
    public string Label { get; } = CreateLabel(variable.Name);

    /// <summary>The variable's help, then its name for the console and the file.</summary>
    public string Help => $"{Variable.Help} ({Variable.Name}, {Variable.DefaultText} by default)";

    /// <summary>Raises the value's change after the variable was set from elsewhere, such as the file changing on disk.</summary>
    public void Refresh()
    {
        OnPropertyChanged(string.Empty);
    }

    protected void Set(string text)
    {
        if (!Variable.TrySet(text))
        {
            return;
        }

        OnPropertyChanged(string.Empty);
        saved();
    }

    private static string CreateLabel(string name)
    {
        int dot = name.LastIndexOf('.');
        string word = name[(dot + 1)..];
        StringBuilder label = new();
        for (int index = 0; index < word.Length; index++)
        {
            char letter = word[index];
            bool startsWord = index > 0 && char.IsUpper(letter) && !char.IsUpper(word[index - 1]);
            if (startsWord)
            {
                label.Append(' ');
                letter = char.ToLowerInvariant(letter);
            }

            label.Append(letter);
        }

        return label.ToString();
    }
}
