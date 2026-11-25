using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Runtime.Debugging;
using FlickerVoid.Engine.Runtime.Settings;

namespace FlickerVoid.Editor.Core.Settings;

/// <summary>
/// The project settings window: its Graphics page lists the game's saved rendering console variables (<c>r.</c>), and setting one
/// sets it in the edited game and writes the project's settings file (<see cref="SettingsFile.FileName"/>), which the game reads
/// when it starts and which can be edited by hand when a setting breaks the editor.
/// </summary>
public sealed partial class ProjectSettingsViewModel : ObservableObject, IDisposable
{
    public const string GraphicsPrefix = "r.";

    private readonly EditorScene _scene;

    public ProjectSettingsViewModel(EditorScene scene)
    {
        _scene = scene;
        _scene.Opened += Rebuild;
        _scene.SettingsChanged += Refresh;
        Graphics = CreateSettings(GraphicsPrefix);
    }

    [ObservableProperty]
    public partial IReadOnlyList<SettingViewModel> Graphics { get; private set; }

    /// <summary>Where the settings are kept, for the person to edit by hand.</summary>
    public string FileNote => $"Kept in the project's content as {SettingsFile.FileName}; remove a line there to put a setting back to its default.";

    /// <summary>Puts every saved setting back to its default and writes the file.</summary>
    [RelayCommand]
    public void ResetToDefaults()
    {
        IEnumerable<ConsoleVariable> variables = _scene.Game.DebugConsole.Entries.OfType<ConsoleVariable>();
        foreach (ConsoleVariable variable in variables.Where(variable => variable.Saved))
        {
            variable.Reset();
        }

        Save();
        Refresh();
    }

    public void Dispose()
    {
        _scene.Opened -= Rebuild;
        _scene.SettingsChanged -= Refresh;
    }

    private List<SettingViewModel> CreateSettings(string prefix)
    {
        IEnumerable<ConsoleVariable> variables = _scene.Game.DebugConsole.Entries.OfType<ConsoleVariable>();
        IEnumerable<ConsoleVariable> shown = variables.Where(variable => variable.Saved && variable.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        List<SettingViewModel> settings = [];
        foreach (ConsoleVariable variable in shown)
        {
            SettingViewModel setting = CreateSetting(variable);
            settings.Add(setting);
        }

        return settings;
    }

    private SettingViewModel CreateSetting(ConsoleVariable variable)
    {
        if (variable.Kind == ConsoleValueKind.Boolean)
        {
            return new SwitchSettingViewModel(variable, Save);
        }

        if (variable.Choices.Count > 0)
        {
            return new ChoiceSettingViewModel(variable, Save);
        }

        return new TextSettingViewModel(variable, Save);
    }

    private void Save()
    {
        string json = SettingsFile.Write(_scene.Game.DebugConsole, SettingsFile.SchemaPath);
        byte[] contents = Encoding.UTF8.GetBytes(json);
        _scene.Content.Write(SettingsFile.FileName, contents);
    }

    private void Rebuild()
    {
        Graphics = CreateSettings(GraphicsPrefix);
    }

    private void Refresh()
    {
        foreach (SettingViewModel setting in Graphics)
        {
            setting.Refresh();
        }
    }
}
