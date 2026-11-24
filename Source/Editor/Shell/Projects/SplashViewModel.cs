using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Projects;

/// <summary>
/// The splash shown while a project opens, after Unreal's: the project's image (black without one), a progress bar and one line
/// saying what the build does now.
/// </summary>
public sealed partial class SplashViewModel(string project, string? image, string status) : ObservableObject
{
    public string Project { get; } = project;

    /// <summary>The image file the project names for its splash; null shows black.</summary>
    public string? Image { get; } = image;

    [ObservableProperty]
    public partial string Status { get; set; } = status;

    /// <summary>The share of the build's projects built, in percent.</summary>
    [ObservableProperty]
    public partial double Percent { get; set; }

    /// <summary>Whether the bar has no end yet, as the build hasn't counted its projects.</summary>
    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; } = true;

    public void Show(BuildProgress progress)
    {
        if (progress.Status.Length > 0)
        {
            Status = progress.Status;
        }

        IsIndeterminate = progress.Total == 0;
        if (progress.Total > 0)
        {
            Percent = 100.0 * progress.Built / progress.Total;
        }
    }
}
