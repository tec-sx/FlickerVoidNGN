using FlickerVoid.Editor.Core.Curves;
using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Editor.Core.Tests.Curves;

public sealed class CurveEditorViewModelTests
{
    private const float Precision = 1e-4f;

    private readonly CurveEditorViewModelTestsBuilder _builder = new();

    [Fact]
    public void SelectsTheKeyUnderThePointer()
    {
        _builder.WithTrack((6, 2), (18, 8));
        CurveEditorViewModel editor = _builder.Build();

        bool hit = editor.Press(182, 21, extend: false);

        hit.Should().BeTrue();
        editor.SelectedKeys.Should().Equal(1);
    }

    [Fact]
    public void AddsToTheSelectionOrTakesAKeyOutOfItWhenExtending()
    {
        _builder.WithTrack((6, 2), (18, 8));
        CurveEditorViewModel editor = _builder.Build();
        editor.Press(60, 80, extend: false);
        editor.Release();

        editor.Press(180, 20, extend: true);
        editor.Release();

        editor.SelectedKeys.Should().Equal(0, 1);

        editor.Press(60, 80, extend: true);
        editor.Release();

        editor.SelectedKeys.Should().Equal(1);
    }

    [Fact]
    public void ClearsTheSelectionOnEmptySpace()
    {
        _builder.WithTrack((6, 2), (18, 8));
        CurveEditorViewModel editor = _builder.Build();
        editor.Press(60, 80, extend: false);
        editor.Release();

        bool hit = editor.Press(120, 50, extend: false);

        hit.Should().BeFalse();
        editor.SelectedKeys.Should().BeEmpty();
    }

    [Fact]
    public void MovesTheSelectedKeysByTheDragWithTheTimeSnappedToAQuarterHour()
    {
        CurveTrack track = _builder.WithTrack((6, 2), (18, 8));
        CurveEditorViewModel editor = _builder.Build();
        editor.Press(60, 80, extend: false);

        editor.Drag(73, 70, snap: true);

        CurveKey moved = track.Curve.Keys[0];
        moved.Time.Should().BeApproximately(7.25f, Precision);
        moved.Value.Should().BeApproximately(3, Precision);
    }

    [Fact]
    public void MovesFreelyWithoutSnapping()
    {
        CurveTrack track = _builder.WithTrack((6, 2), (18, 8));
        CurveEditorViewModel editor = _builder.Build();
        editor.Press(60, 80, extend: false);

        editor.Drag(73, 80, snap: false);

        track.Curve.Keys[0].Time.Should().BeApproximately(7.3f, Precision);
    }

    [Fact]
    public void CommitsADragOnceWhenItIsReleased()
    {
        _builder.WithTrack((6, 2), (18, 8));
        CurveEditorViewModel editor = _builder.Build();
        editor.Press(60, 80, extend: false);
        editor.Drag(70, 80, snap: true);
        editor.Drag(80, 80, snap: true);

        _builder.Commits.Should().BeEmpty("nothing is written while the key is still being dragged");

        editor.Release();

        _builder.Commits.Should().ContainSingle();
        _builder.Commits[0].Keys[0].Time.Should().BeApproximately(8, Precision);
    }

    [Fact]
    public void CommitsNothingForAPressThatDidNotMove()
    {
        _builder.WithTrack((6, 2), (18, 8));
        CurveEditorViewModel editor = _builder.Build();

        editor.Press(60, 80, extend: false);
        editor.Release();

        _builder.Commits.Should().BeEmpty();
    }

    [Fact]
    public void KeepsTheMovedKeySelectedWhenItPassesAnother()
    {
        CurveTrack track = _builder.WithTrack((6, 2), (18, 8));
        CurveEditorViewModel editor = _builder.Build();
        editor.Press(60, 80, extend: false);

        editor.Drag(200, 80, snap: true);
        editor.Release();

        track.Curve.Keys[1].Time.Should().BeApproximately(20, Precision);
        editor.SelectedKeys.Should().Equal(1);
    }

    [Fact]
    public void KeepsKeysWithinTheDayOfALoopingCurve()
    {
        CurveTrack track = _builder.WithTrack((6, 2), (18, 8));
        CurveEditorViewModel editor = _builder.Build();
        editor.Press(180, 20, extend: false);

        editor.Drag(400, 20, snap: true);

        track.Curve.Keys[1].Time.Should().BeLessThan(24);
        track.Curve.Keys[1].Time.Should().BeGreaterThan(23.5f);
    }

    [Fact]
    public void LetsKeysOfACurveWithoutAPeriodGoAnywhere()
    {
        CurveTrack track = _builder.WithTrack(0, (6, 2), (18, 8));
        CurveEditorViewModel editor = _builder.Build();
        editor.Press(180, 20, extend: false);

        editor.Drag(400, 20, snap: true);

        track.Curve.Keys[1].Time.Should().BeApproximately(40, Precision);
    }

    [Fact]
    public void AddsAKeyOnTheLineAtTheSnappedTime()
    {
        CurveTrack track = _builder.WithTrack((6, 2), (18, 2));
        CurveEditorViewModel editor = _builder.Build();

        editor.AddKey(121, 10);

        track.Curve.Keys.Should().HaveCount(3);
        track.Curve.Keys[1].Time.Should().BeApproximately(12, Precision);
        track.Curve.Keys[1].Value.Should().BeApproximately(2, Precision, "the key goes on the line, not where the pointer is");
        editor.SelectedKeys.Should().Equal(1);
        _builder.Commits.Should().ContainSingle();
    }

    [Fact]
    public void AddsAKeyAtThePointerToACurveWithoutKeys()
    {
        CurveTrack track = _builder.WithTrack();
        CurveEditorViewModel editor = _builder.Build();

        editor.AddKey(120, 40);

        track.Curve.Keys.Should().ContainSingle();
        track.Curve.Keys[0].Time.Should().BeApproximately(12, Precision);
        track.Curve.Keys[0].Value.Should().BeApproximately(6, Precision);
    }

    [Fact]
    public void DeletesTheSelectedKeys()
    {
        CurveTrack track = _builder.WithTrack((6, 2), (12, 5), (18, 8));
        CurveEditorViewModel editor = _builder.Build();
        editor.Press(60, 80, extend: false);
        editor.Release();
        editor.Press(180, 20, extend: true);
        editor.Release();

        editor.DeleteSelected();

        track.Curve.Keys.Should().ContainSingle().Which.Time.Should().Be(12);
        editor.SelectedKeys.Should().BeEmpty();
        _builder.Commits.Should().ContainSingle();
    }

    [Fact]
    public void SetsTheModeOfTheSelectedKeys()
    {
        CurveTrack track = _builder.WithTrack((6, 2), (12, 5), (18, 8));
        CurveEditorViewModel editor = _builder.Build();
        editor.Press(120, 50, extend: false);
        editor.Release();

        editor.SetMode(CurveMode.Constant);

        track.Curve.Keys[1].Mode.Should().Be(CurveMode.Constant);
        track.Curve.Keys[0].Mode.Should().Be(CurveMode.Smooth);
        _builder.Commits.Should().ContainSingle();
    }

    [Fact]
    public void GivesAKeyMadeFreeTheSlopeItHad()
    {
        CurveTrack track = _builder.WithTrack((6, 2), (12, 5), (18, 8));
        CurveEditorViewModel editor = _builder.Build();
        float before = track.Curve.Evaluate(13, 24);
        editor.Press(120, 50, extend: false);
        editor.Release();

        editor.SetMode(CurveMode.Free);

        CurveKey key = track.Curve.Keys[1];
        key.In.Should().BeApproximately(0.5f, 0.01f);
        key.Out.Should().BeApproximately(0.5f, 0.01f);
        track.Curve.Evaluate(13, 24).Should().BeApproximately(before, 0.01f);
    }

    [Fact]
    public void ShowsSlopeHandlesOnlyOnAFreeKey()
    {
        _builder.WithTrack(new Curve([
            new CurveKey { Time = 6, Value = 2 },
            new CurveKey { Time = 12, Value = 5, Mode = CurveMode.Free, In = 0, Out = 0 },
        ]));
        CurveEditorViewModel editor = _builder.Build();

        editor.GetHandles(0).Should().BeNull();
        CurveHandles? handles = editor.GetHandles(1);

        handles.Should().NotBeNull();
        handles!.Value.In.X.Should().BeApproximately(120 - CurveEditorViewModel.HandleLength, 1e-3);
        handles.Value.In.Y.Should().BeApproximately(50, 1e-3);
        handles.Value.Out.X.Should().BeApproximately(120 + CurveEditorViewModel.HandleLength, 1e-3);
    }

    [Fact]
    public void TurnsAFreeKeysSlopeByItsHandle()
    {
        CurveTrack track = _builder.WithTrack(new Curve([
            new CurveKey { Time = 6, Value = 2 },
            new CurveKey { Time = 12, Value = 5, Mode = CurveMode.Free, In = 0, Out = 0 },
        ]));
        CurveEditorViewModel editor = _builder.Build();
        double outX = 120 + CurveEditorViewModel.HandleLength;

        editor.Press(outX, 50, extend: false).Should().BeTrue();
        editor.Drag(outX, 50 - CurveEditorViewModel.HandleLength, snap: true);
        editor.Release();

        CurveKey key = track.Curve.Keys[1];
        key.Out.Should().BeApproximately(1, Precision, "a handle at 45 degrees on a view of equal scales is a slope of one");
        key.In.Should().Be(0);
        key.Time.Should().Be(12);
        _builder.Commits.Should().ContainSingle();
    }

    [Fact]
    public void TakesThePointerOnlyOnTheActiveTrack()
    {
        _builder.WithTrack((6, 2));
        CurveTrack second = _builder.WithTrack((18, 8));
        CurveEditorViewModel editor = _builder.Build();

        editor.Press(60, 80, extend: false).Should().BeFalse();
        editor.Press(180, 20, extend: false).Should().BeTrue();
        editor.Active.Should().BeSameAs(second);
    }

    [Fact]
    public void ClearsTheSelectionWhenAnotherTrackBecomesActive()
    {
        CurveTrack first = _builder.WithTrack((6, 2));
        _builder.WithTrack((18, 8));
        CurveEditorViewModel editor = _builder.Build();
        editor.Press(180, 20, extend: false);
        editor.Release();

        editor.Active = first;

        editor.SelectedKeys.Should().BeEmpty();
    }

    [Fact]
    public void FitsTheWholeDayAndTheValuesOfEveryTrack()
    {
        _builder.WithTrack((6, 2));
        _builder.WithTrack((18, 8));
        CurveEditorViewModel editor = _builder.Build();
        editor.Range.Pan(500, 500);

        editor.Fit();

        editor.Range.MinTime.Should().BeApproximately(0, Precision);
        editor.Range.MaxTime.Should().BeApproximately(24, Precision);
        editor.Range.MinValue.Should().BeLessThan(2);
        editor.Range.MaxValue.Should().BeGreaterThan(8);
    }

    [Fact]
    public void DrawsALoopingLinePastTheDaysEnds()
    {
        CurveTrack track = _builder.WithTrack((6, 2), (18, 8));
        CurveEditorViewModel editor = _builder.Build();
        editor.Range.Pan(60, 0);

        IReadOnlyList<CurvePoint> line = editor.GetLine(track, samples: 241);

        line.Should().HaveCount(241);
        line[0].X.Should().BeApproximately(0, 1e-3);
        line[^1].X.Should().BeApproximately(240, 1e-3);
        float wrapped = track.Curve.Evaluate(-6, 24);
        line[0].Y.Should().BeApproximately(editor.Range.ToScreen(-6, wrapped).Y, 1e-3);
    }

    [Fact]
    public void LeavesTheCurveAsItWasWhenNothingIsSelected()
    {
        CurveTrack track = _builder.WithTrack((6, 2));
        CurveEditorViewModel editor = _builder.Build();

        editor.DeleteSelected();
        editor.SetMode(CurveMode.Linear);

        track.Curve.Keys.Should().ContainSingle();
        _builder.Commits.Should().BeEmpty();
    }
}
