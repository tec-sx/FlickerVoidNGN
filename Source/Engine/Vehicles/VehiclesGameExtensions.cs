using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Vehicles.Serialization;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>Adds vehicles to a game.</summary>
public static class VehiclesGameExtensions
{
    /// <summary>
    /// Adds the <c>.fvvehicle</c> file kind and its schema, so the content check reads vehicle files, and the <c>vehicle</c> scene
    /// component, whose parts are spawned from its file through the <see cref="IContentManager"/> service (none without one) and
    /// drawn through a mesh source, after <c>AddRendering</c>. Adding it again does nothing.
    /// </summary>
    public static void AddVehicles(this Game game)
    {
        if (game.Services.GetService<VehicleSpawnSystem>() is not null)
        {
            return;
        }

        game.Schemas.AddFile("vehicle", VehicleFile.Extension, static () => VehiclesJsonContext.Default.VehicleFile);
        game.Spawner.Register("vehicle", VehiclesJsonContext.Default.Vehicle);
        ILogger logger = game.LoggerFactory.CreateLogger(typeof(VehicleFiles));
        VehicleFiles files = new(logger);
        if (game.Services.GetService<MeshSources>() is { } meshes)
        {
            VehicleMeshSource source = new(files);
            meshes.Add(source);
        }

        CreationParts parts = game.AddCreations();
        IContentManager? content = game.Services.GetService<IContentManager>();
        VehicleSpawnSystem spawner = new(game.World, parts, files, content);
        game.FrameSystems.Add(spawner);
        game.Services.AddService(spawner);
    }
}
