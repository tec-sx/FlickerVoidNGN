using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Settings;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Settings;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Core.Tests.Settings;

/// <summary>Builds the project settings window's view model over an edited scene whose content folder may hold a settings file.</summary>
internal sealed class ProjectSettingsViewModelTestsBuilder : IDisposable
{
    private readonly string _contentFolder = Directory.CreateTempSubdirectory().FullName;
    private ContentManager? _content;
    private EditorScene? _scene;
    private ProjectSettingsViewModel? _settings;

    public EditorScene Scene => _scene!;

    public RenderingSettings Rendering => Scene.Game.Services.GetService<RenderingSettings>()!;

    private string SettingsPath => Path.Combine(_contentFolder, SettingsFile.FileName);

    public ProjectSettingsViewModelTestsBuilder WithSettings(string json)
    {
        File.WriteAllText(SettingsPath, json);
        return this;
    }

    public ProjectSettingsViewModel Build()
    {
        _content = new ContentManager(_contentFolder);
        _scene = new EditorScene(NullLoggerFactory.Instance, _content);
        _settings = new ProjectSettingsViewModel(_scene);
        return _settings;
    }

    public string ReadSettings()
    {
        return File.ReadAllText(SettingsPath);
    }

    /// <summary>Writes the file as a person would by hand and reports it changed, as the content watcher does.</summary>
    public void EditSettingsOnDisk(string json)
    {
        File.WriteAllText(SettingsPath, json);
        _content!.NotifyChanged(SettingsPath);
    }

    public void Dispose()
    {
        _settings?.Dispose();
        _scene?.Dispose();
        Directory.Delete(_contentFolder, true);
    }
}
