using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Core.Timelines;
using FlickerVoid.Engine.Cameras.Shots;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Tests.Shots;

public sealed class ShotTimelineViewModelTests : IDisposable
{
    private const float Precision = 1e-3f;

    private readonly ShotTimelineViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsNothingUntilAShotIsSelected()
    {
        Entity lamp = _builder.Add("""{ "name": "Lamp", "components": { "transform": {}, "pointLight": {} } }""");
        _builder.Scene.Selection.Select(lamp);

        _builder.Timeline.HasShot.Should().BeFalse();
        _builder.Timeline.Keys.Should().BeEmpty();
    }

    [Fact]
    public void ListsTheSelectedShotsKeysAlongItsTimeline()
    {
        Entity hall = _builder.AddShot("Hall");

        _builder.Scene.Selection.Select(hall);

        _builder.Timeline.HasShot.Should().BeTrue();
        _builder.Timeline.Duration.Should().Be(4);
        _builder.Timeline.Keys.Select(key => key.Time).Should().Equal(0, 2, 4);
        _builder.Timeline.Keys.Select(key => key.Fraction).Should().Equal(0, 0.5f, 1);
    }

    [Fact]
    public void PreviewsTheShotWhereTheTimelineIsScrubbed()
    {
        Entity hall = _builder.AddShot("Hall");
        _builder.Scene.Selection.Select(hall);

        _builder.Timeline.ScrubTime = 1;
        RenderCamera? preview = _builder.Timeline.GetPreview();

        preview!.Value.Transform.Position.X.Should().BeApproximately(6, Precision);
        preview.Value.Camera.OrthographicHeight.Should().Be(6);
    }

    [Fact]
    public void PreviewsAShotThatLooksAtItsTargetLookingAtIt()
    {
        _builder.Add("""{ "name": "Fox", "components": { "transform": { "position": [1, 0, -5] }, "cameraTarget": {} } }""");
        Entity watcher = _builder.Add("""
            { "name": "Watcher", "components": { "transform": {}, "camera": {}, "shot": { "lookAtTarget": true, "target": "Fox" } } }
            """);
        _builder.Scene.Selection.Select(watcher);

        RenderCamera? preview = _builder.Timeline.GetPreview();

        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, preview!.Value.Transform.Rotation);
        Vector3 expected = Vector3.Normalize(new Vector3(1, 0, -5));
        Vector3.Distance(forward, expected).Should().BeLessThan(Precision);
    }

    [Fact]
    public void AddsAKeyFromAViewAtTheScrubbedTimeInTheShotsSpace()
    {
        Entity hall = _builder.AddShot("Hall");
        _builder.Scene.Selection.Select(hall);
        _builder.Timeline.ScrubTime = 3;
        Transform view = new() { Position = new Vector3(3, 2, 1) };

        _builder.Timeline.AddKey(view);

        List<ShotKey> keys = _builder.Scene.Game.World.Get<Shot>(hall).Keys;
        keys.Select(key => key.Time).Should().Equal(0, 2, 3, 4);
        keys[2].Position.Should().Be(new Vector3(2, 2, 1));
        _builder.Timeline.SelectedKey.Should().Be(2);
        _builder.Timeline.Keys.Should().HaveCount(4);
        _builder.Scene.IsUnsaved.Should().BeTrue();
    }

    [Fact]
    public void RetimesAKeyBetweenItsNeighbours()
    {
        Entity hall = _builder.AddShot("Hall");
        _builder.Scene.Selection.Select(hall);

        _builder.Timeline.MoveKeyTime(1, 3);
        float moved = _builder.Scene.Game.World.Get<Shot>(hall).Keys[1].Time;
        _builder.Timeline.MoveKeyTime(1, 9);
        float clamped = _builder.Scene.Game.World.Get<Shot>(hall).Keys[1].Time;
        _builder.Timeline.MoveKeyTime(0, -1);

        moved.Should().Be(3);
        clamped.Should().Be(4);
        _builder.Scene.Game.World.Get<Shot>(hall).Keys[0].Time.Should().Be(0);
    }

    [Fact]
    public void DeletesTheSelectedKey()
    {
        Entity hall = _builder.AddShot("Hall");
        _builder.Scene.Selection.Select(hall);
        _builder.Timeline.SelectKey(1);

        _builder.Timeline.DeleteSelectedKeyCommand.Execute(null);

        _builder.Scene.Game.World.Get<Shot>(hall).Keys.Select(key => key.Time).Should().Equal(0, 4);
        _builder.Timeline.SelectedKey.Should().BeNull();
    }

    [Fact]
    public void GivesAndTakesTheSelectedKeysWorldTransform()
    {
        Entity hall = _builder.AddShot("Hall");
        _builder.Scene.Selection.Select(hall);
        _builder.Timeline.SelectKey(1);

        _builder.Timeline.TryGetKeyTransform(out Transform before).Should().BeTrue();
        Transform moved = before;
        moved.Position = new Vector3(11, 3, 0);
        _builder.Timeline.SetKeyTransform(moved);

        before.Position.Should().Be(new Vector3(11, 0, 0));
        _builder.Scene.Game.World.Get<Shot>(hall).Keys[1].Position.Should().Be(new Vector3(10, 3, 0));
        _builder.Timeline.SelectedKey.Should().Be(1);
    }

    [Fact]
    public void ForgetsTheKeyWhenAnotherShotIsSelected()
    {
        Entity hall = _builder.AddShot("Hall");
        Entity cellar = _builder.AddShot("Cellar");
        _builder.Scene.Selection.Select(hall);
        _builder.Timeline.SelectKey(1);
        _builder.Timeline.ScrubTime = 2;

        _builder.Scene.Selection.Select(cellar);

        _builder.Timeline.SelectedKey.Should().BeNull();
        _builder.Timeline.ScrubTime.Should().Be(0);
        _builder.Timeline.Shot.Should().Be(cellar);
    }

    [Fact]
    public void GivesTheRailThroughTheKeysInTheWorld()
    {
        Entity hall = _builder.AddShot("Hall");
        _builder.Scene.Selection.Select(hall);

        IReadOnlyList<Vector3> rail = _builder.Timeline.GetRailPoints();
        IReadOnlyList<Vector3> keys = _builder.Timeline.GetKeyPoints();

        rail[0].Should().Be(new Vector3(1, 0, 0));
        Vector3.Distance(rail[^1], new Vector3(21, 0, 0)).Should().BeLessThan(Precision);
        keys.Should().Equal(new Vector3(1, 0, 0), new Vector3(11, 0, 0), new Vector3(21, 0, 0));
    }
    [Fact]
    public void ShowsTheKeysOnOneLaneOfItsTimeline()
    {
        Entity hall = _builder.AddShot("Hall");

        _builder.Scene.Selection.Select(hall);

        TimelineLane lane = _builder.Timeline.Timeline.Lanes.Should().ContainSingle().Subject;
        lane.Items.Select(item => item.Start).Should().Equal(0, 2, 4);
        lane.Items.Should().OnlyContain(item => item.Kind == TimelineKinds.Key);
        _builder.Timeline.Timeline.Scale.Should().BeOfType<SecondScale>();
    }

    [Fact]
    public void RetimesAKeyDraggedOnItsTimeline()
    {
        Entity hall = _builder.AddShot("Hall");
        _builder.Scene.Selection.Select(hall);
        TimelineItem key = _builder.Timeline.Timeline.Lanes[0].Items[1];

        _builder.Timeline.Timeline.Editor!.Apply(new TimelineChange(TimelineChangeKind.MoveItem, key, null, 3, 0));

        _builder.Timeline.Keys.Select(item => item.Time).Should().Equal(0, 3, 4);
    }

    [Fact]
    public void SelectsTheKeyPickedOnItsTimeline()
    {
        Entity hall = _builder.AddShot("Hall");
        _builder.Scene.Selection.Select(hall);
        TimelineItem key = _builder.Timeline.Timeline.Lanes[0].Items[2];

        _builder.Timeline.Timeline.Select(key);

        _builder.Timeline.SelectedKey.Should().Be(2);
    }

    [Fact]
    public void MarksTheSelectedKeyOnItsTimeline()
    {
        Entity hall = _builder.AddShot("Hall");
        _builder.Scene.Selection.Select(hall);

        _builder.Timeline.SelectKey(1);

        _builder.Timeline.Timeline.SelectedItems.Select(item => item.Source).Should().Equal(1);
    }

    [Fact]
    public void DeletesTheKeySelectedOnItsTimeline()
    {
        Entity hall = _builder.AddShot("Hall");
        _builder.Scene.Selection.Select(hall);
        _builder.Timeline.Timeline.Select(_builder.Timeline.Timeline.Lanes[0].Items[1]);

        TimelineEdit.DeleteSelected(_builder.Timeline.Timeline);

        _builder.Timeline.Keys.Select(item => item.Time).Should().Equal(0, 4);
    }

    [Fact]
    public void ScrubsWithItsTimelinesCursor()
    {
        Entity hall = _builder.AddShot("Hall");
        _builder.Scene.Selection.Select(hall);

        _builder.Timeline.ScrubTime = 1.5f;
        double cursor = _builder.Timeline.Timeline.Cursor;
        _builder.Timeline.Timeline.MoveCursor(2.5);

        cursor.Should().BeApproximately(1.5, Precision);
        _builder.Timeline.ScrubTime.Should().BeApproximately(2.5f, Precision);
    }

    [Fact]
    public void ShowsTheWholeShotOnItsTimeline()
    {
        Entity hall = _builder.AddShot("Hall");
        _builder.Timeline.Timeline.Range.Resize(216);

        _builder.Scene.Selection.Select(hall);

        _builder.Timeline.Timeline.Range.ToX(0).Should().BeApproximately(8, Precision);
        _builder.Timeline.Timeline.Range.ToX(4).Should().BeApproximately(208, Precision);
    }
}
