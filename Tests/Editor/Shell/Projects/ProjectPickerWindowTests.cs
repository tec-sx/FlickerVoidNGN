using Avalonia.Controls;
using Avalonia.Threading;

using FlickerVoid.Editor.Projects;

namespace FlickerVoid.Editor.Tests.Projects;

public sealed class ProjectPickerWindowTests : IDisposable
{
    private readonly ProjectPickerWindowTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public async Task ShowsTheSplashOverThePickerWhileTheProjectBuildsAndClosesItWithThePicker()
    {
        await _builder.WithBuildWaiting().Run(async window =>
        {
            Task opening = _builder.Picker.OpenFile(_builder.ProjectFile);
            SplashWindow splash = window.OwnedWindows.OfType<SplashWindow>().Single();
            bool shownWhileBuilding = splash.IsVisible;
            _builder.FinishBuild();
            await opening;
            window.Close();

            shownWhileBuilding.Should().BeTrue();
            splash.IsVisible.Should().BeFalse();
        });
    }

    [Fact]
    public async Task AProjectWithoutAnImageShowsTheEditorsStar()
    {
        await _builder.WithBuildWaiting().Run(async window =>
        {
            Task opening = _builder.Picker.OpenFile(_builder.ProjectFile);
            SplashWindow splash = window.OwnedWindows.OfType<SplashWindow>().Single();
            Image picture = splash.GetControl<Image>("Picture");
            bool pictured = picture.Source is not null;
            _builder.FinishBuild();
            await opening;

            pictured.Should().BeTrue();
        });
    }

    [Fact]
    public async Task AFailedOpenClosesTheSplashAndShowsTheLog()
    {
        await _builder.WithFailingBuild("Door.cs(3,1): error CS1002: ; expected").Run(async window =>
        {
            await _builder.Picker.OpenFile(_builder.ProjectFile);
            Dispatcher.UIThread.RunJobs();

            window.OwnedWindows.OfType<SplashWindow>().Should().NotContain(splash => splash.IsVisible);
            ProjectFailureWindow failure = window.OwnedWindows.OfType<ProjectFailureWindow>().Single();
            TextBox log = failure.GetControl<TextBox>("Log");
            log.Text.Should().Contain("error CS1002");
            failure.Close();
        });
    }
}
