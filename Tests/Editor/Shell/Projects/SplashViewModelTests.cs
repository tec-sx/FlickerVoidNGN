using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;

namespace FlickerVoid.Editor.Tests.Projects;

public sealed class SplashViewModelTests
{
    private readonly SplashViewModelTestsBuilder _builder = new();

    [Fact]
    public void ShowsWhatTheBuildIsDoingAndHowFarItCame()
    {
        SplashViewModel splash = _builder.Build();

        splash.Show(new BuildProgress(9, 27, "Building FlickerVoid.Engine.Runtime"));

        splash.Status.Should().Be("Building FlickerVoid.Engine.Runtime");
        splash.Percent.Should().BeApproximately(33.3, 0.1);
        splash.IsIndeterminate.Should().BeFalse();
    }

    [Fact]
    public void TheBarHasNoEndUntilTheBuildCountsItsProjects()
    {
        SplashViewModel splash = _builder.Build();

        splash.Show(new BuildProgress(0, 0, "Reading the projects Hollow.Launcher builds"));

        splash.IsIndeterminate.Should().BeTrue();
    }

    [Fact]
    public void StartsWithWhatOpeningDoesFirst()
    {
        SplashViewModel splash = _builder.Build();

        splash.Status.Should().Be("Opening Hollow");
        splash.IsIndeterminate.Should().BeTrue();
    }
}
