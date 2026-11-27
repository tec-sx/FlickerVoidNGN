using Arch.Core;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Interaction.Interactables;
using FlickerVoid.Engine.Interaction.Toggles;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Props.Serialization;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Props;

/// <summary>Adds props to a game.</summary>
public static class PropsGameExtensions
{
    /// <summary>
    /// Adds the <c>.fvprop</c> file kind and its schema and the <c>prop</c> scene component, whose parts are spawned from its file
    /// through the <see cref="IContentManager"/> service (none without one) and drawn through a mesh source, after
    /// <c>AddRendering</c>. Adding it again does nothing.
    /// </summary>
    public static void AddProps(this Game game)
    {
        if (game.Services.GetService<PropSpawnSystem>() is not null)
        {
            return;
        }

        ArrayRegistry.Add<StaticBody>();
        ArrayRegistry.Add<RigidBody>();
        ArrayRegistry.Add<UsePoint>();
        ArrayRegistry.Add<Toggle>();
        game.Schemas.AddFile("prop", PropFile.Extension, static () => PropsJsonContext.Default.PropFile);
        game.Spawner.Register("prop", PropsJsonContext.Default.Prop);
        ILogger logger = game.LoggerFactory.CreateLogger(typeof(PropFile));
        CreationFiles<PropFile> files = new(logger, PropsJsonContext.Default.PropFile);
        if (game.Services.GetService<MeshSources>() is { } meshes)
        {
            PropMeshSource source = new(files);
            meshes.Add(source);
        }

        CreationParts parts = game.AddCreations();
        IContentManager? content = game.Services.GetService<IContentManager>();
        PropSpawnSystem spawner = new(game.World, parts, files, content);
        game.FrameSystems.Add(spawner);
        game.Services.AddService(spawner);
    }
}
