using Arch.Core;

using FlickerVoid.Engine.Buildings.Serialization;
using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Buildings;

/// <summary>Adds buildings to a game.</summary>
public static class BuildingsGameExtensions
{
    /// <summary>
    /// Adds the <c>.fvbuilding</c> file kind and its schema, so the content check reads building files, and the <c>building</c> scene
    /// component, whose parts are spawned from its file through the <see cref="IContentManager"/> service (none without one) and
    /// drawn through a mesh source, after <c>AddRendering</c>; and the system that runs elevators. Adding it again does nothing.
    /// </summary>
    public static void AddBuildings(this Game game)
    {
        if (game.Services.GetService<BuildingSpawnSystem>() is not null)
        {
            return;
        }

        ArrayRegistry.Add<StaticBody>();
        ArrayRegistry.Add<RigidBody>();
        ArrayRegistry.Add<Elevator>();
        ArrayRegistry.Add<UsePoint>();
        ArrayRegistry.Add<Toggle>();
        ArrayRegistry.Add<IndoorVolume>();
        game.Schemas.AddFile("building", BuildingFile.Extension, static () => BuildingsJsonContext.Default.BuildingFile);
        game.Spawner.Register("building", BuildingsJsonContext.Default.Building);
        ILogger logger = game.LoggerFactory.CreateLogger(typeof(BuildingFile));
        CreationFiles<BuildingFile> files = new(logger, BuildingsJsonContext.Default.BuildingFile);
        if (game.Services.GetService<MeshSources>() is { } meshes)
        {
            BuildingMeshSource source = new(files);
            meshes.Add(source);
        }

        CreationParts parts = game.AddCreations();
        IContentManager? content = game.Services.GetService<IContentManager>();
        BuildingSpawnSystem spawner = new(game.World, parts, files, content);
        game.FrameSystems.Add(spawner);
        game.Services.AddService(spawner);
        ElevatorSystem elevators = new(game.World, game.Events);
        game.Services.AddService(elevators);
    }
}
