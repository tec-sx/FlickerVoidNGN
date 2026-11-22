using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Maps.Navigation;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Maps.Tests.Navigation;

/// <summary>
/// Builds a playing game with maps over a content folder holding marker kinds (a shop on every view, a pin always on the minimap's
/// edge, a secret hidden until found within 3 metres, a quest target shown while <c>Fact.Quest</c> is set, the waypoint) and two maps
/// (the town, 100 metres square, and the inn inside it from 0 to 10, open while <c>Fact.InnOpen</c> is set), with a navigator at the
/// origin whose minimap shows 20 metres.
/// </summary>
internal sealed class MapSystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private DataAssets? _data;

    public MapSystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
        WithFile("Markers/Shop.fvdata", """
            { "type": "marker", "id": "Marker.Shop", "tags": [ "Marker.Category.Shop" ], "priority": 1,
              "fragments": [ { "$type": "minimap" }, { "$type": "compass" }, { "$type": "worldMap" } ] }
            """);
        WithFile("Markers/Pin.fvdata", """{ "type": "marker", "id": "Marker.Pin", "fragments": [ { "$type": "minimap", "clampToEdge": true } ] }""");
        WithFile("Markers/Secret.fvdata", """
            { "type": "marker", "id": "Marker.Secret", "fragments": [ { "$type": "minimap" },
              { "$type": "discovery", "radius": 3, "hiddenUntilDiscovered": true,
                "onDiscovered": { "effects": [ { "$type": "fact", "fact": "Fact.Reward" } ] } } ] }
            """);
        WithFile("Markers/Quest.fvdata", """
            { "type": "marker", "id": "Marker.Quest", "fragments": [ { "$type": "minimap" } ],
              "visibleWhen": { "conditions": [ { "$type": "fact", "fact": "Fact.Quest" } ] } }
            """);
        WithFile("Markers/Waypoint.fvdata", """{ "type": "marker", "id": "Marker.Waypoint", "fragments": [ { "$type": "minimap" }, { "$type": "compass" } ] }""");
        WithFile("Maps/Town.fvdata", """{ "type": "map", "id": "Map.Town", "layers": [ { "name": "Town", "min": [-50, -50], "max": [50, 50] } ] }""");
        WithFile("Maps/Inn.fvdata", """
            { "type": "map", "id": "Map.Inn", "priority": 1, "layers": [ { "name": "Hall", "min": [0, 0], "max": [10, 10] } ],
              "availableWhen": { "conditions": [ { "$type": "fact", "fact": "Fact.InnOpen" } ] } }
            """);
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public Entity Navigator { get; private set; }

    public MapSystem Build()
    {
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        MapSystem maps = Game.AddMaps(new MapSettings { WaypointMarker = "Markers/Waypoint.fvdata" });
        Game.Lifecycle.Play(Game.World);
        Navigator = Spawn("""
            { "name": "Fox", "components": { "transform": {}, "navigator": { "minimap": { "radius": 20, "minRadius": 5, "maxRadius": 50 } } } }
            """);
        return maps;
    }

    /// <summary>Puts a marker of the given kind on a new entity at a place, with the fact it sets once discovered.</summary>
    public Entity AddMarker(string kind, Vector3 position, string label = "", string discoveredFact = "")
    {
        string json = $$"""
            { "name": "{{kind}}", "components": { "transform": { "position": [{{position.X}}, {{position.Y}}, {{position.Z}}] },
              "mapMarker": { "definition": "Markers/{{kind}}.fvdata", "label": "{{label}}", "discoveredFact": "{{discoveredFact}}" } } }
            """;
        return Spawn(json);
    }

    public void MoveNavigator(Vector3 position)
    {
        ref Transform transform = ref Game.World.Get<Transform>(Navigator);
        transform.Position = position;
    }

    public List<T> Collect<T>()
        where T : struct
    {
        List<T> sent = [];
        Game.Events.Handle((in T value) => sent.Add(value));
        return sent;
    }

    public void Run(float seconds)
    {
        int steps = (int)MathF.Ceiling(seconds * 60);
        for (int step = 0; step < steps; step++)
        {
            Game.Tick(Game.Loop.FixedStep);
        }
    }

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private void WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
    }

    private Entity Spawn(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        return Game.Spawner.Spawn(scene, Game.World)[0];
    }
}
