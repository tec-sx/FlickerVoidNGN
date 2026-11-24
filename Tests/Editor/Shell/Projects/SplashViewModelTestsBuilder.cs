using FlickerVoid.Editor.Projects;

namespace FlickerVoid.Editor.Tests.Projects;

/// <summary>Builds the splash of a project without an image, as it starts opening.</summary>
internal sealed class SplashViewModelTestsBuilder
{
    public SplashViewModel Build() => new("Hollow", null, "Opening Hollow");
}
