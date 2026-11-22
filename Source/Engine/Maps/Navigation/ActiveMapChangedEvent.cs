using Arch.Core;

using FlickerVoid.Engine.Maps.Definitions;

namespace FlickerVoid.Engine.Maps.Navigation;

/// <summary>The map or layer under the navigator changed; the map is null outside every map.</summary>
public readonly record struct ActiveMapChangedEvent(Entity Navigator, MapDefinition? Map, int LayerIndex);
