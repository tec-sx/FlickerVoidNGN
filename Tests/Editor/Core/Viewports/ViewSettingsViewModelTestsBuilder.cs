using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Editor.Core.Tests.Viewports;

/// <summary>Builds the view settings over a new lighting mood, with the content files given to <see cref="WithFiles"/>.</summary>
internal sealed class ViewSettingsViewModelTestsBuilder
{
    private readonly List<string> _files = [];

    public LightingMood Mood { get; } = new();

    public ViewSettingsViewModelTestsBuilder WithFiles(params string[] paths)
    {
        _files.AddRange(paths);
        return this;
    }

    public ViewSettingsViewModel Build() => new(Mood, Find);

    private IReadOnlyList<string> Find(IReadOnlyList<string> extensions)
    {
        return [.. _files.Where(path => extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))];
    }
}
