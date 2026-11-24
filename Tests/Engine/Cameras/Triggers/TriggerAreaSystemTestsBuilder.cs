using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Triggers;

namespace FlickerVoid.Engine.Cameras.Tests.Triggers;

/// <summary>Builds a playing game with a trigger area, two metres wide, centred one metre right of its entity at the origin, and activators to walk through it.</summary>
internal sealed class TriggerAreaSystemTestsBuilder : IDisposable
{
    public CameraTestGame Game { get; } = new();

    public Entity Area { get; private set; }

    /// <summary>Spawns the area, needing the tags given from whatever sets it off.</summary>
    public TriggerAreaSystemTestsBuilder WithArea(string requiredTags = "")
    {
        Area = Game.Spawn($$"""
            { "name": "Window", "components": { "transform": {}, "triggerArea": { "size": [2, 2, 2], "offset": [1, 0, 0], "requiredTags": [ {{requiredTags}} ] } } }
            """);
        return this;
    }

    public TriggerAreaSystem Triggers => Game.Game.Services.GetService<TriggerAreaSystem>()!;

    /// <summary>Spawns the fox at the position, an activator unless told not to and saved by the id when one is given.</summary>
    public Entity SpawnActivator(Vector3 position, bool activator = true, string saveId = "")
    {
        string marker = activator ? """, "triggerActivator": {}""" : "";
        string saveable = saveId.Length > 0 ? $$""", "saveable": { "id": "{{saveId}}" }""" : "";
        return Game.Spawn($$"""
            { "name": "Fox", "components": { "transform": { "position": [{{position.X}}, {{position.Y}}, {{position.Z}}] } {{marker}} {{saveable}} } }
            """);
    }

    public void Dispose() => Game.Dispose();
}
