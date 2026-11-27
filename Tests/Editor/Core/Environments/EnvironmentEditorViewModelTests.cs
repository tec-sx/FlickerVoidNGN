using System.Numerics;

using FlickerVoid.Editor.Core.Environments;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Editor.Core.Tests.Environments;

public sealed class EnvironmentEditorViewModelTests
{
    private const float Precision = 1e-4f;

    private readonly EnvironmentEditorViewModelTestsBuilder _builder = new();

    [Fact]
    public void GroupsTheValuesAnEnvironmentDrawsUnderSunSkyFogWeatherAndGrade()
    {
        EnvironmentEditorViewModel editor = _builder.Build();

        editor.Groups.Select(group => group.Name).Should().Equal("Sun", "Sky", "Fog", "Weather", "Grade");
        IEnumerable<string> names = editor.Groups.SelectMany(group => group.Values).Select(value => value.Name);
        names.Should().Contain(["exposure", "fogColor", "rain", "sunAngle", "tint"]);
        names.Should().NotContain(["bloom", "sunDirection", "depthNearBurn"], "the lens's values and the sun's path are not the environment's to draw");
    }

    [Fact]
    public void ShowsEachValueAtTheCursorsHour()
    {
        EnvironmentEditorViewModel editor = _builder.Build();

        editor.Hour = 6;

        NumberFieldViewModel exposure = GetNumber(editor, "exposure");
        exposure.Value.Should().BeApproximately(0.6, Precision);

        editor.Hour = 12;

        exposure.Value.Should().BeApproximately(1, Precision);
    }

    [Fact]
    public void MarksTheValuesTheEnvironmentDraws()
    {
        EnvironmentEditorViewModel editor = _builder.Build();

        editor.Find("exposure").IsDrawn.Should().BeTrue();
        editor.Find("fogColor").IsDrawn.Should().BeTrue();
        editor.Find("contrast").IsDrawn.Should().BeFalse();
    }

    [Fact]
    public void WritesAKeyAtTheSnappedHourWhenAValueIsEdited()
    {
        EnvironmentEditorViewModel editor = _builder.Build();
        editor.Hour = 6.1f;

        GetNumber(editor, "exposure").Value = 0.9;

        IReadOnlyList<CurveKey> keys = _builder.Environment.Numbers["exposure"].Keys;
        keys.Should().HaveCount(3);
        keys[1].Time.Should().Be(6);
        keys[1].Value.Should().BeApproximately(0.9f, Precision);
        _builder.Edits.Should().Be(1);
    }

    [Fact]
    public void ReplacesTheKeyAlreadyAtThatHourKeepingItsMode()
    {
        EnvironmentEditorViewModel editor = _builder.Build();
        editor.Hour = 12;

        GetNumber(editor, "exposure").Value = 2;

        IReadOnlyList<CurveKey> keys = _builder.Environment.Numbers["exposure"].Keys;
        keys.Should().HaveCount(2);
        keys[1].Value.Should().Be(2);
        keys[1].Mode.Should().Be(CurveMode.Linear);
    }

    [Fact]
    public void StartsACurveForAValueTheEnvironmentDidNotDraw()
    {
        EnvironmentEditorViewModel editor = _builder.Build();
        editor.Hour = 18;

        GetNumber(editor, "contrast").Value = 1.4;

        _builder.Environment.Numbers["contrast"].Keys.Should().ContainSingle().Which.Time.Should().Be(18);
        editor.Find("contrast").IsDrawn.Should().BeTrue();
    }

    [Fact]
    public void WritesAColorKeyWhenAColorIsEdited()
    {
        EnvironmentEditorViewModel editor = _builder.Build();
        editor.Hour = 20;

        ColorFieldViewModel fog = (ColorFieldViewModel)editor.Find("fogColor").Field;
        fog.Color = new Vector3(0.1f, 0.1f, 0.2f);

        IReadOnlyList<ColorKey> keys = _builder.Environment.Colors["fogColor"].Keys;
        keys.Should().HaveCount(2);
        keys[1].Time.Should().Be(20);
        keys[1].Color.X.Should().BeApproximately(0.0103f, 1e-3f, "the field picks sRGB and the mood holds linear colors");
        keys[1].Color.Z.Should().BeApproximately(0.0331f, 1e-3f);
    }

    [Fact]
    public void ShowsTheSelectedNumbersAsCurvesAndColorsAsStrips()
    {
        EnvironmentEditorViewModel editor = _builder.Build();

        editor.Find("exposure").IsSelected = true;
        editor.Find("fogColor").IsSelected = true;

        editor.Curves.Tracks.Should().ContainSingle().Which.Name.Should().Be("exposure");
        editor.Curves.Active.Should().BeSameAs(editor.Curves.Tracks[0]);
        editor.ColorTracks.Should().ContainSingle().Which.Name.Should().Be("fogColor");

        editor.Find("exposure").IsSelected = false;

        editor.Curves.Tracks.Should().BeEmpty();
        editor.Curves.Active.Should().BeNull();
    }

    [Fact]
    public void EachValueKnowsWhichShownCurveIsItsOwn()
    {
        EnvironmentEditorViewModel editor = _builder.Build();

        editor.Find("exposure").IsSelected = true;
        editor.Find("contrast").IsSelected = true;
        int second = editor.Find("contrast").TrackIndex;
        editor.Find("exposure").IsSelected = false;

        second.Should().Be(1);
        editor.Find("contrast").TrackIndex.Should().Be(0);
        editor.Find("exposure").TrackIndex.Should().Be(EnvironmentValueViewModel.NoTrack);
    }

    [Fact]
    public void StartsOnTheFirstGroupsTab()
    {
        EnvironmentEditorViewModel editor = _builder.Build();

        editor.SelectedGroup.Should().BeSameAs(editor.Groups[0]);
    }

    [Fact]
    public void WritesACurveEditedInTheViewToTheEnvironment()
    {
        EnvironmentEditorViewModel editor = _builder.Build();
        editor.Find("exposure").IsSelected = true;
        editor.Curves.Fit();

        editor.Curves.AddKey(180, 0);

        _builder.Environment.Numbers["exposure"].Keys.Should().HaveCount(3);
        _builder.Edits.Should().Be(1);
    }

    [Fact]
    public void ShowsAFieldsEditOnTheShownCurve()
    {
        EnvironmentEditorViewModel editor = _builder.Build();
        editor.Find("exposure").IsSelected = true;
        editor.Hour = 18;

        GetNumber(editor, "exposure").Value = 0.5;

        editor.Curves.Tracks[0].Curve.Keys.Should().HaveCount(3);
    }

    [Fact]
    public void RemovesAValuesCurve()
    {
        EnvironmentEditorViewModel editor = _builder.Build();
        editor.Find("exposure").IsSelected = true;

        editor.RemoveCurve(editor.Find("exposure"));

        _builder.Environment.Numbers.Should().NotContainKey("exposure");
        editor.Find("exposure").IsDrawn.Should().BeFalse();
        editor.Find("exposure").IsSelected.Should().BeFalse();
        editor.Curves.Tracks.Should().BeEmpty();
        _builder.Edits.Should().Be(1);
    }

    [Fact]
    public void PlaysTheHourOnAtItsSpeedAroundTheDay()
    {
        EnvironmentEditorViewModel editor = _builder.Build();
        editor.Hour = 22;
        editor.Speed = 2;
        editor.IsPlaying = true;

        editor.Advance(3);

        editor.Hour.Should().BeApproximately(4, Precision);
    }

    [Fact]
    public void HoldsTheHourWhileNotPlaying()
    {
        EnvironmentEditorViewModel editor = _builder.Build();
        editor.Hour = 22;

        editor.Advance(3);

        editor.Hour.Should().Be(22);
    }

    [Fact]
    public void ShowsTheHourAsAClock()
    {
        EnvironmentEditorViewModel editor = _builder.Build();

        editor.Hour = 6.5f;

        editor.HourText.Should().Be("06:30");
    }

    private static NumberFieldViewModel GetNumber(EnvironmentEditorViewModel editor, string name)
    {
        return (NumberFieldViewModel)editor.Find(name).Field;
    }
}
