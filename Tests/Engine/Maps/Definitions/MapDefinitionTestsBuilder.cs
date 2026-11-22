using System.Numerics;

using FlickerVoid.Engine.Maps.Definitions;

namespace FlickerVoid.Engine.Maps.Tests.Definitions;

/// <summary>Builds a map of a town 100 metres square around the origin with a cellar under part of it, below height -1.</summary>
internal sealed class MapDefinitionTestsBuilder
{
    public MapLayer Town { get; } = new() { Name = "Town", Min = new Vector2(-50, -50), Max = new Vector2(50, 50) };

    public MapLayer Cellar { get; } = new() { Name = "Cellar", Min = new Vector2(0, 0), Max = new Vector2(10, 20), LimitHeight = true, MinHeight = -5, MaxHeight = -1 };

    public MapDefinition Build() => new() { Layers = [Town, Cellar] };
}
