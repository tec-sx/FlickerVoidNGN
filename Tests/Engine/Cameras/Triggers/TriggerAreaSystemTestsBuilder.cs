using System.Numerics;

using Arch.Core;

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

    public Entity SpawnActivator(Vector3 position, bool activator = true)
    {
        string marker = activator ? """, "triggerActivator": {}""" : "";
        return Game.Spawn($$"""
            { "name": "Fox", "components": { "transform": { "position": [{{position.X}}, {{position.Y}}, {{position.Z}}] } {{marker}} } }
            """);
    }

    public void Dispose() => Game.Dispose();
}
