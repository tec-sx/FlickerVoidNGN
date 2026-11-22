using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Viewports;
using FlickerVoid.Engine.Rendering.PostProcessing;

namespace FlickerVoid.Editor.Core.Tests.Viewports;

public sealed class ViewSettingsViewModelTests
{
    private readonly ViewSettingsViewModelTestsBuilder _builder = new();

    [Fact]
    public void ShowsAFieldForEverySettingOfTheMoodButItsSchema()
    {
        ViewSettingsViewModel settings = _builder.Build();

        string[] labels = [.. settings.Fields.Select(field => field.Label)];
        labels.Should().Contain(["Sun Direction", "Exposure", "Tonemapper", "White Point"]);
        labels.Should().NotContain(label => label.Contains("Schema"));
    }

    [Fact]
    public void PicksTheTonemapperFromItsValuesWithUncharted2First()
    {
        ViewSettingsViewModel settings = _builder.Build();

        EnumFieldViewModel tonemapper = settings.Fields.OfType<EnumFieldViewModel>().Single(field => field.Label == "Tonemapper");

        tonemapper.Value.Should().Be(Tonemapper.Uncharted2);
        tonemapper.Options.Should().Contain([Tonemapper.None, Tonemapper.Uncharted2, Tonemapper.Aces]);
    }

    [Fact]
    public void EditsTheMoodInPlace()
    {
        ViewSettingsViewModel settings = _builder.Build();

        settings.Fields.OfType<NumberFieldViewModel>().Single(field => field.Label == "Exposure").Value = 2;
        settings.Fields.OfType<EnumFieldViewModel>().Single(field => field.Label == "Tonemapper").Value = Tonemapper.Aces;

        _builder.Mood.Exposure.Should().Be(2);
        _builder.Mood.Tonemapper.Should().Be(Tonemapper.Aces);
    }

    [Fact]
    public void RaisesEditedAfterEachChangeToTheMood()
    {
        ViewSettingsViewModel settings = _builder.Build();
        List<float> exposures = [];
        settings.Edited += () => exposures.Add(_builder.Mood.Exposure);

        NumberFieldViewModel exposure = settings.Fields.OfType<NumberFieldViewModel>().Single(field => field.Label == "Exposure");
        exposure.Value = 2;
        exposure.Value = 3;

        exposures.Should().Equal(2, 3);
    }

    [Fact]
    public void PicksColoursAndLimitsFromTheMoodsAttributes()
    {
        ViewSettingsViewModel settings = _builder.Build();

        settings.Fields.Single(field => field.Label == "Tint").Should().BeOfType<ColorFieldViewModel>();
        NumberFieldViewModel exposure = settings.Fields.OfType<NumberFieldViewModel>().Single(field => field.Label == "Exposure");
        exposure.Maximum.Should().Be(8);
    }

    [Fact]
    public void PicksTheCanvasAmongTheCanvasFiles()
    {
        ViewSettingsViewModel settings = _builder.WithFiles("Paper.png", "Canvases/Paper.fvcanvas", "Canvases/Linen.fvcanvas", "Moods/Day.fvmood").Build();

        AssetFieldViewModel canvas = settings.Fields.OfType<AssetFieldViewModel>().Single(field => field.Label == "Canvas");
        canvas.Picker.IsOpen = true;

        canvas.Picker.Matches.Should().BeEquivalentTo("Canvases/Paper.fvcanvas", "Canvases/Linen.fvcanvas");
    }
}
