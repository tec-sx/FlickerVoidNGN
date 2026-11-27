using Arch.Core;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Foliage.Serialization;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Foliage;

/// <summary>Adds foliage to a game.</summary>
public static class FoliageGameExtensions
{
    /// <summary>
    /// Adds the <c>.fvfoliage</c> file kind and its schema, so the content check reads foliage files, and the <c>foliage</c> and
    /// <c>foliageScatter</c> scene components, whose parts are spawned from their file through the <see cref="IContentManager"/>
    /// service (none without one) and drawn through a mesh source, after <c>AddRendering</c>. Adding it again does nothing.
    /// </summary>
    public static void AddFoliage(this Game game)
    {
        if (game.Services.GetService<FoliageSpawnSystem>() is not null)
        {
            return;
        }

        ArrayRegistry.Add<StaticBody>();
        game.Schemas.AddFile("foliage", FoliageFile.Extension, static () => FoliageJsonContext.Default.FoliageFile);
        game.Spawner.Register("foliage", FoliageJsonContext.Default.Plant);
        game.Spawner.Register("foliageScatter", FoliageJsonContext.Default.FoliageScatter);
        ILogger logger = game.LoggerFactory.CreateLogger(typeof(FoliageFile));
        CreationFiles<FoliageFile> files = new(logger, FoliageJsonContext.Default.FoliageFile);
        if (game.Services.GetService<MeshSources>() is { } meshes)
        {
            FoliageMeshSource source = new(files);
            meshes.Add(source);
        }

        CreationParts parts = game.AddCreations();
        IContentManager? content = game.Services.GetService<IContentManager>();
        FoliageSpawnSystem spawner = new(game.World, parts, files, content);
        game.FrameSystems.Add(spawner);
        game.Services.AddService(spawner);
    }
}
