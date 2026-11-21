using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Abilities.Activation;
using FlickerVoid.Engine.Attributes;
using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Abilities.Tests.Activation;

/// <summary>
/// Builds a playing game with attributes and abilities over a hero whose set grants Sneak (held on Input.Sneak, costs 2 stamina,
/// owns Status.Sneaking), Talk (exclusive, replaceable, blocked while sneaking), Hide (cancels movement, blocks social abilities),
/// Examine (instant, sets a fact, blocks exclusive abilities while it runs) and any abilities a test adds; and a guard as target.
/// </summary>
internal sealed class AbilitySystemTestsBuilder : IDisposable
{
    private readonly string _folder;
    private readonly List<string> _extraAbilities = [];
    private DataAssets? _data;

    public AbilitySystemTestsBuilder()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _folder = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_folder);
        WithFile("Abilities/Sneak.fvdata", """
            { "type": "ability", "id": "Ability.Sneak", "tags": [ "Ability.Movement" ], "policy": "WhileInputHeld",
              "ownedTagsWhileActive": [ "Status.Sneaking" ],
              "costs": [ { "$type": "attributeCost", "attribute": "Attribute.Stamina", "amount": 2 } ],
              "failureMessages": [ { "reason": "Ability.Failure.Cost", "text": "Too tired to sneak." } ] }
            """);
        WithFile("Abilities/Talk.fvdata", """
            { "type": "ability", "id": "Ability.Talk", "tags": [ "Ability.Social" ], "group": "ExclusiveReplace", "blockedTags": [ "Status.Sneaking" ] }
            """);
        WithFile("Abilities/Hide.fvdata", """
            { "type": "ability", "id": "Ability.Hide", "cancelAbilitiesWithTags": [ "Ability.Movement" ], "blockAbilitiesWithTags": [ "Ability.Social" ] }
            """);
        WithFile("Abilities/Examine.fvdata", """
            { "type": "ability", "id": "Ability.Examine", "instant": true,
              "onActivated": { "effects": [ { "$type": "fact", "fact": "Fact.Examined", "value": 1 } ] } }
            """);
    }

    public static readonly Tag Sneak = new("Ability.Sneak");
    public static readonly Tag Talk = new("Ability.Talk");
    public static readonly Tag Hide = new("Ability.Hide");
    public static readonly Tag Examine = new("Ability.Examine");
    public static readonly Tag SneakInput = new("Input.Sneak");
    public static readonly Tag Stamina = new("Attribute.Stamina");

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public AbilitySystem Abilities { get; private set; } = null!;

    public AttributeSystem Attributes { get; private set; } = null!;

    public Entity Hero { get; private set; }

    public Entity Guard { get; private set; }

    public AbilitySystemTestsBuilder WithFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string? folder = Path.GetDirectoryName(fullPath);
        Directory.CreateDirectory(folder!);
        File.WriteAllText(fullPath, json);
        return this;
    }

    /// <summary>Adds an ability file to the hero's set.</summary>
    public AbilitySystemTestsBuilder WithAbility(string name, string json)
    {
        string path = $"Abilities/{name}.fvdata";
        _extraAbilities.Add(path);
        return WithFile(path, json);
    }

    /// <summary>Starts play with the hero, its stamina and extra <c>abilities</c> properties (such as <c>relationships</c>) given.</summary>
    public AbilitySystemTestsBuilder Build(int stamina = 5, string abilities = "")
    {
        IEnumerable<string> extra = _extraAbilities.Select(path => $$""", { "ability": "{{path}}" }""");
        string entries = string.Concat(extra);
        WithFile("Sets/Hero.fvdata", $$"""
            { "type": "abilitySet", "abilities": [
                { "ability": "Abilities/Sneak.fvdata", "input": "Input.Sneak" },
                { "ability": "Abilities/Talk.fvdata", "input": "Input.Use" },
                { "ability": "Abilities/Hide.fvdata" },
                { "ability": "Abilities/Examine.fvdata" }{{entries}} ] }
            """);
        ContentManager content = new(_folder);
        _data = new DataAssets(content, Game.Data);
        Game.Services.AddService(_data);
        Attributes = Game.AddAttributes();
        Abilities = Game.AddAbilities();
        Game.Lifecycle.Play(Game.World);
        string separator = abilities.Length > 0 ? ", " : string.Empty;
        string json = $$"""
            { "entities": [
                { "components": {
                    "attributes": { "values": [ { "attribute": "Attribute.Stamina", "value": {{stamina}} } ] },
                    "abilities": { "sets": [ "Sets/Hero.fvdata" ]{{separator}}{{abilities}} } } },
                { "components": { } } ] }
            """;
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> spawned = Game.Spawner.Spawn(scene, Game.World);
        Hero = spawned[0];
        Guard = spawned[1];
        return this;
    }

    public bool HeroHasTag(string tag)
    {
        Tag wanted = new(tag);
        return Game.World.TryGet(Hero, out OwnedTags owned) && owned.HasMatchingTag(wanted);
    }

    public ConditionContext Context()
    {
        WorldEntityView hero = new(Game.World, Hero);
        WorldEntityView guard = new(Game.World, Guard);
        return new ConditionContext(Game.Facts) { Instigator = hero, Target = guard, Services = Game.Services };
    }

    public List<T> Collect<T>()
        where T : struct
    {
        List<T> sent = [];
        Game.Events.Handle((in T value) => sent.Add(value));
        return sent;
    }

    public void Dispatch() => Game.Events.Dispatch();

    public void Dispose()
    {
        Game.Dispose();
        _data?.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
