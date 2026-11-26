using System.Numerics;

using FlickerVoid.Editor.Core.Curves;
using FlickerVoid.Engine.Runtime.Curves;

namespace FlickerVoid.Editor.Core.Tests.Curves;

public sealed class ColorTrackTests
{
    private const float Precision = 1e-4f;

    private static readonly Vector3 Night = new(0.1f, 0.1f, 0.3f);
    private static readonly Vector3 Noon = new(1, 0.9f, 0.8f);

    private readonly ColorTrackTestsBuilder _builder = new();

    [Fact]
    public void SelectsTheKeyNearestATimeWithinReach()
    {
        ColorTrack track = _builder.Build((6, Night), (18, Noon));

        track.Press(17.8, reach: 0.5).Should().BeTrue();

        track.Selected.Should().Be(1);

        track.Press(12, reach: 0.5).Should().BeFalse();

        track.Selected.Should().BeNull();
    }

    [Fact]
    public void MovesTheSelectedKeyInTimeSnappedAndCommitsOnRelease()
    {
        ColorTrack track = _builder.Build((6, Night), (18, Noon));
        track.Press(6, reach: 0.5);

        track.Drag(7.1, snap: true);

        track.Curve.Keys[0].Time.Should().BeApproximately(7, Precision);
        _builder.Commits.Should().BeEmpty();

        track.Release();

        _builder.Commits.Should().ContainSingle();
    }

    [Fact]
    public void FollowsTheMovedKeyPastAnother()
    {
        ColorTrack track = _builder.Build((6, Night), (18, Noon));
        track.Press(6, reach: 0.5);

        track.Drag(20, snap: true);

        track.Selected.Should().Be(1);
        track.Curve.Keys[1].Color.Should().Be(Night);
    }

    [Fact]
    public void AddsAKeyWithTheColorAlreadyThere()
    {
        ColorTrack track = _builder.Build((6, Night), (18, Noon));
        Vector3 there = track.Curve.Evaluate(12, 24);

        track.AddKey(12.1);

        track.Curve.Keys.Should().HaveCount(3);
        track.Curve.Keys[1].Time.Should().BeApproximately(12, Precision);
        track.Curve.Keys[1].Color.Should().Be(there);
        track.Selected.Should().Be(1);
        _builder.Commits.Should().ContainSingle();
    }

    [Fact]
    public void SetsTheSelectedKeysColor()
    {
        ColorTrack track = _builder.Build((6, Night), (18, Noon));
        track.Press(18, reach: 0.5);
        track.Release();

        track.SetColor(Vector3.One);

        track.Curve.Keys[1].Color.Should().Be(Vector3.One);
        _builder.Commits.Should().ContainSingle();
    }

    [Fact]
    public void SetsTheSelectedKeysMode()
    {
        ColorTrack track = _builder.Build((6, Night), (18, Noon));
        track.Press(6, reach: 0.5);
        track.Release();

        track.SetMode(CurveMode.Constant);

        track.Curve.Keys[0].Mode.Should().Be(CurveMode.Constant);
    }

    [Fact]
    public void DeletesTheSelectedKey()
    {
        ColorTrack track = _builder.Build((6, Night), (18, Noon));
        track.Press(6, reach: 0.5);
        track.Release();

        track.DeleteSelected();

        track.Curve.Keys.Should().ContainSingle().Which.Color.Should().Be(Noon);
        track.Selected.Should().BeNull();
        _builder.Commits.Should().ContainSingle();
    }

    [Fact]
    public void DoesNothingWithoutASelection()
    {
        ColorTrack track = _builder.Build((6, Night));

        track.SetColor(Vector3.One);
        track.DeleteSelected();
        track.Drag(10, snap: true);
        track.Release();

        track.Curve.Keys.Should().ContainSingle().Which.Color.Should().Be(Night);
        _builder.Commits.Should().BeEmpty();
    }
}
