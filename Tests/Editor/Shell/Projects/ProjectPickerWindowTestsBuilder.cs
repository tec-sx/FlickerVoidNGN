using Avalonia.Headless;

using FlickerVoid.Editor.Projects;

namespace FlickerVoid.Editor.Tests.Projects;

/// <summary>Shows the project picker in a headless window over one project, Hollow, with a build that only pretends.</summary>
internal sealed class ProjectPickerWindowTestsBuilder : IDisposable
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(App)));

    private readonly ProjectPickerViewModelTestsBuilder _picker = new();
    private readonly ManualResetEventSlim _finish = new();

    public ProjectPickerViewModel Picker { get; private set; } = null!;

    public string ProjectFile => _picker.ProjectFile("Hollow");

    /// <summary>Makes the build wait for <see cref="FinishBuild"/>, so the test sees the picker while it builds.</summary>
    public ProjectPickerWindowTestsBuilder WithBuildWaiting()
    {
        _picker.WithBuildWaitingFor(_finish);
        return this;
    }

    public ProjectPickerWindowTestsBuilder WithFailingBuild(string output)
    {
        _picker.WithFailingBuild(output);
        return this;
    }

    public void FinishBuild() => _finish.Set();

    /// <summary>Runs <paramref name="test"/> on the headless UI thread with the picker shown, and closes the picker after it if the test didn't.</summary>
    public async Task Run(Func<ProjectPickerWindow, Task> test)
    {
        await Session.Value.Dispatch(
            async () =>
            {
                Picker = _picker.WithProject("Hollow").Build();
                ProjectPickerWindow window = new() { DataContext = Picker };
                window.Show();
                await test(window);
                if (window.IsVisible)
                {
                    window.Close();
                }

                return true;
            },
            CancellationToken.None);
    }

    public void Dispose()
    {
        _finish.Set();
        _finish.Dispose();
        _picker.Dispose();
    }
}
