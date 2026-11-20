using FlickerVoid.Engine.Editor.Viewports;
using FlickerVoid.Engine.Rendering.Lighting;

namespace FlickerVoid.Engine.Editor.Tests.Viewports;

/// <summary>Builds the view settings over a new lighting mood.</summary>
internal sealed class ViewSettingsViewModelTestsBuilder
{
    public LightingMood Mood { get; } = new();

    public ViewSettingsViewModel Build() => new(Mood);
}
