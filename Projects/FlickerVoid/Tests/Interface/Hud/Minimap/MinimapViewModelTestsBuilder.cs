using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Maps;
using FlickerVoid.Engine.Maps.Markers;
using FlickerVoid.Engine.Maps.Navigation;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Interface.Hud.Minimap;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Interface.Tests.Hud.Minimap;

/// <summary>
/// Builds a minimap over a playing game with maps, a camera and, optionally, the protagonist as the navigator, each turned about the
/// vertical axis, and shop markers (a kind shown on the minimap) at places.
/// </summary>
internal sealed class MinimapViewModelTestsBuilder : IDisposable
{
    private readonly string _folder;
    private readonly DataAssets _data;
    private readonly MapSystem _maps;
    private readonly List<Entity> _markers = [];

    public MinimapViewModelTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
        string shop = Path.Combine(_folder, "Shop.fvdata");
        File.WriteAllText(shop, """{ "type": "marker", "id": "Marker.Shop", "fragments": [ { "$type": "minimap" } ] }""");
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        _maps = Game.AddMaps();
        Game.Lifecycle.Play(Game.World);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public MinimapViewModelTestsBuilder WithCamera(float yawDegrees)
    {
        Quaternion yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, float.DegreesToRadians(yawDegrees));
        Quaternion pitch = Quaternion.CreateFromAxisAngle(Vector3.UnitX, float.DegreesToRadians(-30));
        Transform transform = new() { Position = new Vector3(0, 6, 9), Rotation = yaw * pitch };
        Game.World.Create(transform, new Camera());
        return this;
    }

    public MinimapViewModelTestsBuilder WithMarker(Vector3 position)
    {
        Transform transform = new() { Position = position };
        Entity marker = Game.World.Create(transform, new MapMarker { Definition = "Shop.fvdata" });
        Game.Lifecycle.Start(Game.World, marker);
        _markers.Add(marker);
        return this;
    }

    public MinimapViewModelTestsBuilder WithProtagonist(Vector3 position, float yawDegrees)
    {
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, float.DegreesToRadians(yawDegrees));
        Transform transform = new() { Position = position, Rotation = rotation };
        Entity protagonist = Game.World.Create(transform, new Gameplay.Protagonist.Protagonist(), new Navigator());
        Game.Lifecycle.Start(Game.World, protagonist);
        return this;
    }

    /// <summary>The player follows the marker added at the given place in the order they were added.</summary>
    public MinimapViewModelTestsBuilder Tracking(int marker)
    {
        MarkerHandle handle = _maps.FindMarker(_markers[marker]);
        _maps.SetTracked(handle);
        return this;
    }

    public MinimapViewModel Build() => new(Game.World, _maps);

    public void Dispose()
    {
        Game.Dispose();
        _data.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
