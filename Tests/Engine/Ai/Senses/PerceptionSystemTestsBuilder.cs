using System.Globalization;
using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Ai.Senses;
using FlickerVoid.Engine.Characters;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Ai.Tests.Senses;

/// <summary>
/// Builds a playing game with physics and AI, a guard at the origin facing +Z (sight 8, losing sight at 10, 60 degrees either side,
/// hearing 10, remembering noises for 2 seconds) and records what it perceives.
/// </summary>
internal sealed class PerceptionSystemTestsBuilder : IDisposable
{
    private const string GuardJson = """
        { "name": "Guard", "components": { "transform": {},
          "perception": { "sightRadius": 8, "loseSightRadius": 10, "sightAngle": 60, "hearingRange": 10, "memory": 2INTERVAL }BODY } }
        """;

    private const string Body = """, "staticBody": { "collider": { "type": "box", "size": [0.6, 1.8, 0.4], "offset": [0, 0.9, 0] } }""";

    private bool _body;
    private float? _sightInterval;

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public Entity Guard { get; private set; }

    public List<TargetSeenEvent> Seen { get; } = [];

    public List<TargetLostEvent> Lost { get; } = [];

    public List<NoiseHeardEvent> Heard { get; } = [];

    public List<NoiseForgottenEvent> Forgotten { get; } = [];

    /// <summary>Gives the guard a body of its own around its eyes.</summary>
    public PerceptionSystemTestsBuilder WithBody()
    {
        _body = true;
        return this;
    }

    /// <summary>Makes guards look every so many seconds instead of at the component's default.</summary>
    public PerceptionSystemTestsBuilder WithSightInterval(float seconds)
    {
        _sightInterval = seconds;
        return this;
    }

    public PerceptionSystemTestsBuilder Build()
    {
        Game.AddPhysics();
        Game.AddCharacters();
        Game.AddAi();
        Game.Events.Handle((in TargetSeenEvent sent) => Seen.Add(sent));
        Game.Events.Handle((in TargetLostEvent sent) => Lost.Add(sent));
        Game.Events.Handle((in NoiseHeardEvent sent) => Heard.Add(sent));
        Game.Events.Handle((in NoiseForgottenEvent sent) => Forgotten.Add(sent));
        Game.Lifecycle.Play(Game.World);
        Guard = AddGuard();
        return this;
    }

    /// <summary>Another guard standing where the first does, looking the same way.</summary>
    public Entity AddGuard()
    {
        string body = "";
        if (_body)
        {
            body = Body;
        }

        string interval = "";
        if (_sightInterval is { } seconds)
        {
            interval = string.Create(CultureInfo.InvariantCulture, $""", "sightInterval": {seconds}""");
        }

        string withBody = GuardJson.Replace("BODY", body, StringComparison.Ordinal);
        string guard = withBody.Replace("INTERVAL", interval, StringComparison.Ordinal);
        return SpawnPlaying(guard);
    }

    public Entity AddTarget(Vector3 position)
    {
        string json = $$"""{ "name": "Fox", "components": { "transform": { "position": [{{position.X}}, {{position.Y}}, {{position.Z}}] }, "perceivable": {} } }""";
        return SpawnPlaying(json);
    }

    /// <summary>A wall across the guard's view at the given distance ahead.</summary>
    public Entity AddWall(float ahead)
    {
        string json = $$"""{ "name": "Wall", "components": { "transform": { "position": [0, 1, {{ahead}}] }, "staticBody": { "collider": { "type": "box", "size": [6, 2, 0.2] } } } }""";
        return SpawnPlaying(json);
    }

    public void MoveTo(Entity entity, Vector3 position)
    {
        ref Transform transform = ref Game.World.Get<Transform>(entity);
        transform.Position = position;
    }

    public void Remove(Entity entity) => Game.Lifecycle.Destroy(Game.World, entity);

    public void Run(float seconds)
    {
        int steps = (int)MathF.Ceiling(seconds * 60);
        for (int step = 0; step < steps; step++)
        {
            Game.Tick(Game.Loop.FixedStep);
        }
    }

    public void Dispose() => Game.Dispose();

    private Entity SpawnPlaying(string entityJson)
    {
        string json = $$"""{ "entities": [ {{entityJson}} ] }""";
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        Entity entity = Game.Spawner.Spawn(scene, Game.World)[0];
        Game.Lifecycle.Start(Game.World, entity);
        return entity;
    }
}
