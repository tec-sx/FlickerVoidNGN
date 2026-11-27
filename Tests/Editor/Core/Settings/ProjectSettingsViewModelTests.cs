using FlickerVoid.Editor.Core.Settings;
using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Editor.Core.Tests.Settings;

public sealed class ProjectSettingsViewModelTests : IDisposable
{
    private readonly ProjectSettingsViewModelTestsBuilder _builder = new();

    [Fact]
    public void GraphicsListsTheSavedRenderingVariablesNamedForPeople()
    {
        ProjectSettingsViewModel settings = _builder.Build();

        IEnumerable<string> labels = settings.Graphics.Select(setting => setting.Label);

        labels.Should().Equal(
            "Local lights", "Probes", "Ray tracing", "Live probes", "Upscaling", "Viewport upscaling", "Ray reconstruction", "Depth fades", "VSync");
        settings.Graphics[2].Should().BeOfType<SwitchSettingViewModel>();
        settings.Graphics[4].Should().BeOfType<ChoiceSettingViewModel>().Which.Choices.Should().Equal("Off", "Quality", "Dlaa");
        settings.Graphics[2].Help.Should().EndWith("(r.RayTracing, true by default)");
    }

    [Fact]
    public void TheEditedGameStartsWithTheFilesSettings()
    {
        ProjectSettingsViewModel settings = _builder.WithSettings("""{ "r.RayTracing": false, "r.ViewportUpscaling": "Dlaa" }""").Build();

        RenderingSettings rendering = _builder.Rendering;
        rendering.RayTracing.Value.Should().BeFalse();
        rendering.ViewportUpscaling.Value.Should().Be(UpscaleMode.Dlaa);
        settings.Graphics.OfType<SwitchSettingViewModel>().Single(setting => setting.Label == "Ray tracing").IsOn.Should().BeFalse();
    }

    [Fact]
    public void SwitchingASettingSetsTheGameAndWritesTheFile()
    {
        ProjectSettingsViewModel settings = _builder.Build();
        SwitchSettingViewModel rayTracing = settings.Graphics.OfType<SwitchSettingViewModel>().Single(setting => setting.Label == "Ray tracing");
        ChoiceSettingViewModel upscaling = settings.Graphics.OfType<ChoiceSettingViewModel>().Single(setting => setting.Label == "Upscaling");

        rayTracing.IsOn = false;
        upscaling.Selected = "Dlaa";

        _builder.Rendering.RayTracing.Value.Should().BeFalse();
        _builder.ReadSettings().ReplaceLineEndings("\n").Should().Be("""
            {
              "$schema": ".schemas/settings.schema.json",
              "r.RayTracing": false,
              "r.Upscaling": "Dlaa"
            }

            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ResettingPutsEverySettingBackAndEmptiesTheFile()
    {
        ProjectSettingsViewModel settings = _builder.WithSettings("""{ "r.RayTracing": false }""").Build();

        settings.ResetToDefaults();

        _builder.Rendering.RayTracing.Value.Should().BeTrue();
        _builder.ReadSettings().Should().NotContain("r.");
    }

    [Fact]
    public void AHandEditWhileTheEditorRunsSetsTheGameAndRefreshesThePage()
    {
        ProjectSettingsViewModel settings = _builder.Build();
        SwitchSettingViewModel probes = settings.Graphics.OfType<SwitchSettingViewModel>().Single(setting => setting.Label == "Probes");
        List<string?> changed = [];
        probes.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _builder.EditSettingsOnDisk("""{ "r.Probes": false }""");

        _builder.Rendering.Probes.Value.Should().BeFalse();
        probes.IsOn.Should().BeFalse();
        changed.Should().NotBeEmpty();
    }

    [Fact]
    public void ReloadingTheGameKeepsTheSettingsAndListsTheNewGamesVariables()
    {
        ProjectSettingsViewModel settings = _builder.WithSettings("""{ "r.LiveProbes": false }""").Build();
        SettingViewModel before = settings.Graphics[0];

        _builder.Scene.Reload([]);

        _builder.Rendering.LiveProbes.Value.Should().BeFalse();
        settings.Graphics[0].Should().NotBeSameAs(before);
    }

    public void Dispose()
    {
        _builder.Dispose();
    }
}
