using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Cutaways;

namespace FlickerVoid.Engine.Cameras.Tests.Cutaways;

/// <summary>
/// Builds a playing game with a two-storey house (a ground floor and an upper floor as indoor volumes, a wall on each, the
/// ceiling between them and a roof), a shed's roof beside it, a door the cameras may look at, and the fox the live shot
/// follows, for the cutaway system.
/// </summary>
internal sealed class CutawaySystemTestsBuilder : IDisposable
{
    private readonly CameraTestGame _game = new();
    private readonly Dictionary<string, Entity> _entities = [];
    private Entity _fox;

    public CutawaySystemTestsBuilder WithHouse()
    {
        Add("""{ "name": "House", "components": { "transform": {} } }""");
        Add("""{ "name": "Ground", "parent": "House", "components": { "transform": {}, "indoorVolume": { "size": [6, 3, 6] } } }""");
        Add("""{ "name": "Ground Wall", "parent": "Ground", "components": { "transform": { "position": [0, 0, 3] }, "greybox": { "size": [6, 3, 0.2] } } }""");
        Add("""{ "name": "Ceiling", "parent": "Ground", "components": { "transform": { "position": [0, 3, 0] }, "greybox": { "size": [6, 0.2, 6] } } }""");
        Add("""{ "name": "Upper", "parent": "House", "components": { "transform": { "position": [0, 3.2, 0] }, "indoorVolume": { "size": [6, 3, 6] } } }""");
        Add("""{ "name": "Upper Wall", "parent": "Upper", "components": { "transform": { "position": [0, 3.2, 3] }, "greybox": { "size": [6, 3, 0.2] } } }""");
        Add("""{ "name": "Roof", "parent": "House", "components": { "transform": { "position": [0, 6.2, 0] }, "greybox": { "size": [7, 0.3, 7] } } }""");
        Add("""{ "name": "Shed Roof", "components": { "transform": { "position": [10, 4, 0] }, "greybox": { "size": [3, 0.2, 3] } } }""");
        Add("""{ "name": "Door", "components": { "transform": { "position": [30, 0, 0] }, "cameraTarget": {} } }""");
        _fox = Add("""{ "name": "Fox", "components": { "transform": { "position": [20, 0, 0] }, "cameraTarget": {} } }""");
        _game.SpawnShot("Street", 0, extra: """, "target": "Fox" """);
        return this;
    }

    public World World => _game.Game.World;

    public Entity this[string name] => _entities[name];

    public CutawaySystem Build() => _game.Game.Services.GetService<CutawaySystem>()!;

    /// <summary>Puts the fox at <paramref name="position"/> and runs a frame.</summary>
    public void WalkTo(Vector3 position)
    {
        _game.MoveTo(_fox, position);
        _game.Run(1 / 60f);
    }

    /// <summary>Gives the fox another component, moving it to another archetype, as stopping or starting a walk does, and runs a frame.</summary>
    public void ChangeFoxArchetype()
    {
        World.Add(_fox, new Marker());
        _game.Run(1 / 60f);
    }

    private readonly record struct Marker;

    public void Dispose() => _game.Dispose();

    private Entity Add(string json)
    {
        Entity entity = _game.Spawn(json);
        string name = json.Split('"')[3];
        _entities.Add(name, entity);
        return entity;
    }
}
