using FlickerVoid.Engine.Maps.Definitions;
using FlickerVoid.Engine.Maps.Markers;
using FlickerVoid.Engine.Maps.Navigation;
using FlickerVoid.Engine.Maps.Serialization;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Maps;

/// <summary>Adds maps to a game: the <c>map</c> and <c>marker</c> data asset types and the marker fragments, the <c>mapMarker</c> and <c>navigator</c> components, and the map system (a fixed system and service).</summary>
public static class MapsGameExtensions
{
    public static MapSystem AddMaps(this Game game, MapSettings? settings = null)
    {
        game.Data.AddResolver(MapsJsonContext.Default);
        game.Data.Register<MapDefinition>("map");
        game.Data.Register<MarkerDefinition>("marker");
        game.Data.RegisterDerived<MarkerFragment, MinimapFragment>("minimap");
        game.Data.RegisterDerived<MarkerFragment, CompassFragment>("compass");
        game.Data.RegisterDerived<MarkerFragment, WorldMapFragment>("worldMap");
        game.Data.RegisterDerived<MarkerFragment, DiscoveryFragment>("discovery");
        game.Spawner.Register("mapMarker", MapsJsonContext.Default.MapMarker);
        game.Spawner.Register("navigator", MapsJsonContext.Default.Navigator);

        MapSystem maps = new(game, settings ?? new MapSettings());
        game.FixedSystems.Add(maps);
        game.Services.AddService(maps);
        return maps;
    }
}
