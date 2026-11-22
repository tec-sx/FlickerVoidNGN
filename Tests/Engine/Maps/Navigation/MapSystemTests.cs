using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Maps.Definitions;
using FlickerVoid.Engine.Maps.Markers;
using FlickerVoid.Engine.Maps.Navigation;

namespace FlickerVoid.Engine.Maps.Tests.Navigation;

public sealed class MapSystemTests : IDisposable
{
    private const float North = 0;
    private const float East = 90;

    private readonly MapSystemTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsMarkersOnTheMinimapAtTheirOffsetTurnedWithTheView()
    {
        MapSystem maps = _builder.Build();
        Entity shop = _builder.AddMarker("Shop", new Vector3(10, 0, -10), "Fishmonger");
        _builder.Run(0.3f);

        MinimapView facingNorth = maps.BuildMinimapView(North);
        MinimapView facingEast = maps.BuildMinimapView(East);

        MarkerView marker = facingNorth.Markers.Should().ContainSingle().Subject;
        marker.Handle.Should().Be(maps.FindMarker(shop));
        marker.Label.Should().Be("Fishmonger");
        marker.Position.X.Should().BeApproximately(0.5f, 1e-4f);
        marker.Position.Y.Should().BeApproximately(-0.5f, 1e-4f);
        marker.Distance.Should().BeApproximately(MathF.Sqrt(200), 1e-3f);
        facingNorth.Radius.Should().Be(20);
        facingNorth.PlayerRotation.Should().BeApproximately(180, 1e-3f, "the navigator faces +Z, south");
        Vector2 turned = facingEast.Markers.Single().Position;
        turned.X.Should().BeApproximately(-0.5f, 1e-4f);
        turned.Y.Should().BeApproximately(-0.5f, 1e-4f);
        facingEast.MapRotation.Should().Be(-90);
    }

    [Fact]
    public void LeavesOutMarkersBeyondTheMinimapUnlessTheyArePinnedOrTracked()
    {
        MapSystem maps = _builder.Build();
        Entity far = _builder.AddMarker("Shop", new Vector3(0, 0, -100));
        _builder.AddMarker("Pin", new Vector3(100, 0, 0));
        _builder.Run(0.3f);

        MinimapView untracked = maps.BuildMinimapView(North);
        maps.SetTracked(maps.FindMarker(far));
        MinimapView tracked = maps.BuildMinimapView(North);

        MarkerView pin = untracked.Markers.Should().ContainSingle().Subject;
        pin.Position.Should().Be(new Vector2(1, 0));
        pin.Clamped.Should().BeTrue();
        MarkerView followed = tracked.Markers.Should().ContainSingle(view => view.Tracked).Subject;
        followed.Position.Y.Should().BeApproximately(-1, 1e-4f);
        followed.Clamped.Should().BeTrue();
    }

    [Fact]
    public void TheCompassShowsBearingsWithinItsFieldOfView()
    {
        MapSystem maps = _builder.Build();
        _builder.AddMarker("Shop", new Vector3(10, 0, -10));
        Entity behind = _builder.AddMarker("Shop", new Vector3(0, 0, 10));
        _builder.Run(0.3f);

        CompassView ahead = maps.BuildCompassView(North);
        maps.SetTracked(maps.FindMarker(behind));
        CompassView withTracked = maps.BuildCompassView(North);

        ahead.Heading.Should().Be(0);
        MarkerView shop = ahead.Markers.Should().ContainSingle().Subject;
        shop.Position.X.Should().BeApproximately(0.5f, 1e-4f);
        shop.Rotation.Should().BeApproximately(45, 1e-3f);
        withTracked.Markers.Should().HaveCount(2);
        MarkerView pinned = withTracked.Markers.Single(view => view.Tracked);
        MathF.Abs(pinned.Position.X).Should().Be(1);
        pinned.Clamped.Should().BeTrue();
    }

    [Fact]
    public void DiscoversHiddenPlacesWhenTheNavigatorComesNear()
    {
        MapSystem maps = _builder.Build();
        List<MarkerDiscoveredEvent> discovered = _builder.Collect<MarkerDiscoveredEvent>();
        Entity secret = _builder.AddMarker("Secret", new Vector3(10, 0, 0), discoveredFact: "Fact.Found.Secret");
        _builder.Run(0.3f);
        MinimapView before = maps.BuildMinimapView(North);

        _builder.MoveNavigator(new Vector3(8, 0, 0));
        _builder.Run(0.3f);

        before.Markers.Should().BeEmpty();
        MarkerHandle handle = maps.FindMarker(secret);
        maps.IsDiscovered(handle).Should().BeTrue();
        _builder.Game.Facts.GetFact(new Tag("Fact.Found.Secret")).Should().Be(1);
        _builder.Game.Facts.GetFact(new Tag("Fact.Reward")).Should().Be(1);
        discovered.Should().ContainSingle().Which.Marker.Should().Be(handle);
        maps.BuildMinimapView(North).Markers.Should().ContainSingle().Which.Discovered.Should().BeTrue();
    }

    [Fact]
    public void ShowsMarkersOnlyWhileTheirConditionsHold()
    {
        MapSystem maps = _builder.Build();
        List<MarkerVisibilityChangedEvent> changed = _builder.Collect<MarkerVisibilityChangedEvent>();
        Entity quest = _builder.AddMarker("Quest", new Vector3(5, 0, 0));
        _builder.Run(0.3f);
        MarkerHandle handle = maps.FindMarker(quest);
        bool before = maps.IsVisible(handle);

        _builder.Game.Facts.SetFact(new Tag("Fact.Quest"), 1);
        _builder.Run(0.3f);

        before.Should().BeFalse();
        maps.IsVisible(handle).Should().BeTrue();
        changed.Should().ContainSingle().Which.Marker.Should().Be(handle);
        maps.BuildMinimapView(North).Markers.Should().ContainSingle();
    }

    [Fact]
    public void AWaypointReplacesTheLastOneAndIsTracked()
    {
        MapSystem maps = _builder.Build();

        MarkerHandle first = maps.SetWaypoint(new Vector3(5, 0, 5));
        MarkerHandle second = maps.SetWaypoint(new Vector3(-5, 0, 5));

        maps.Waypoint.Should().Be(second);
        maps.Tracked.Should().Be(second);
        maps.Contains(first).Should().BeFalse();
        maps.GetLocation(second).Should().Be(new Vector3(-5, 0, 5));
        maps.ClearWaypoint();
        maps.Contains(second).Should().BeFalse();
        maps.Waypoint.IsValid.Should().BeFalse();
        maps.Tracked.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FollowsTheMapUnderTheNavigator()
    {
        MapSystem maps = _builder.Build();
        List<ActiveMapChangedEvent> changes = _builder.Collect<ActiveMapChangedEvent>();
        _builder.Run(0.3f);
        Tag town = maps.ActiveMap!.Id;

        _builder.MoveNavigator(new Vector3(5, 0, 5));
        _builder.Run(0.3f);
        Tag closed = maps.ActiveMap!.Id;
        _builder.Game.Facts.SetFact(new Tag("Fact.InnOpen"), 1);
        _builder.Run(0.3f);

        town.Should().Be(new Tag("Map.Town"));
        closed.Should().Be(new Tag("Map.Town"), "the inn's map is only used while it is open");
        maps.ActiveMap!.Id.Should().Be(new Tag("Map.Inn"));
        maps.ActiveLayer.Should().Be(0);
        changes.Select(change => change.Map!.Id).Should().Equal(new Tag("Map.Town"), new Tag("Map.Inn"));
    }

    [Fact]
    public void TheWorldMapShowsTheMarkersOnALayerAndThePlayer()
    {
        MapSystem maps = _builder.Build();
        _builder.AddMarker("Shop", new Vector3(10, 0, -10));
        _builder.AddMarker("Shop", new Vector3(200, 0, 0));
        _builder.Run(0.3f);
        MapDefinition town = maps.Maps.Single(map => map.Id == new Tag("Map.Town"));

        WorldMapView view = maps.BuildWorldMapView(town, 0);

        view.PlayerOnLayer.Should().BeTrue();
        view.PlayerUv.Should().Be(new Vector2(0.5f, 0.5f));
        MarkerView shop = view.Markers.Should().ContainSingle().Subject;
        shop.Position.X.Should().BeApproximately(0.6f, 1e-4f);
        shop.Position.Y.Should().BeApproximately(0.4f, 1e-4f);
    }

    [Fact]
    public void HiddenCategoriesLeaveMarkersOutUnlessTracked()
    {
        MapSystem maps = _builder.Build();
        Entity shop = _builder.AddMarker("Shop", new Vector3(5, 0, 0));
        _builder.Run(0.3f);

        maps.SetCategoryHidden(new Tag("Marker.Category"), true);
        MinimapView hidden = maps.BuildMinimapView(North);
        maps.SetTracked(maps.FindMarker(shop));
        MinimapView tracked = maps.BuildMinimapView(North);

        hidden.Markers.Should().BeEmpty();
        tracked.Markers.Should().ContainSingle();
    }

    [Fact]
    public void MarkersLeaveWithTheirEntities()
    {
        MapSystem maps = _builder.Build();
        List<MarkerRemovedEvent> removed = _builder.Collect<MarkerRemovedEvent>();
        Entity shop = _builder.AddMarker("Shop", new Vector3(5, 0, 0));
        _builder.Run(0.1f);
        MarkerHandle handle = maps.FindMarker(shop);
        maps.SetTracked(handle);

        _builder.Game.Lifecycle.Destroy(_builder.Game.World, shop);
        _builder.Run(0.1f);

        maps.Contains(handle).Should().BeFalse();
        maps.Tracked.IsValid.Should().BeFalse();
        removed.Should().ContainSingle().Which.Marker.Should().Be(handle);
    }
}
