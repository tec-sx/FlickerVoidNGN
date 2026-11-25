
using Arch.Core;

using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Rendering.Outlines;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.PostProcessing;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Rendering;

/// <summary>Adds rendering to a game.</summary>
public static class RenderingGameExtensions
{
    /// <summary>Lets scene files add "camera", "meshRenderer", "meshAnimator", "pointLight", "spotLight", "irradianceVolume", "sceneMood", "sceneEnvironment", "outline", "mainCamera", "indoorVolume", "uncut" and "doorway" components, which a <see cref="SceneRenderer"/> draws, a <see cref="MeshAnimationSystem"/> to the frame systems, the <see cref="MeshSources"/> service, the <see cref="SceneRendererSetup"/> service (which gives renderers those sources), and the mood, environment, lens, material, canvas and texture settings file kinds with their schemas.</summary>
    public static void AddRendering(this Game game)
    {
        game.Spawner.Register("camera", RenderingJsonContext.Default.Camera);
        game.Spawner.Register("meshRenderer", RenderingJsonContext.Default.MeshRenderer);
        game.Spawner.Register("meshAnimator", RenderingJsonContext.Default.MeshAnimator);
        game.Spawner.Register("pointLight", RenderingJsonContext.Default.PointLight);
        game.Spawner.Register("spotLight", RenderingJsonContext.Default.SpotLight);
        game.Spawner.Register("irradianceVolume", RenderingJsonContext.Default.IrradianceVolume);
        game.Spawner.Register("sceneMood", RenderingJsonContext.Default.SceneMood);
        game.Spawner.Register("sceneEnvironment", RenderingJsonContext.Default.SceneEnvironment);
        game.Spawner.Register("outline", RenderingJsonContext.Default.Outline);
        game.Spawner.Register("mainCamera", RenderingJsonContext.Default.MainCamera);
        game.Spawner.Register("indoorVolume", RenderingJsonContext.Default.IndoorVolume);
        game.Spawner.Register("uncut", RenderingJsonContext.Default.Uncut);
        game.Spawner.Register("doorway", RenderingJsonContext.Default.Doorway);
        game.Schemas.AddFile("mood", LightingMood.Extension, static () => RenderingJsonContext.Default.LightingMood);
        game.Schemas.AddFile("environment", LightingEnvironment.Extension, static () => RenderingJsonContext.Default.LightingEnvironment);
        game.Schemas.AddFile("lens", CameraLens.Extension, static () => RenderingJsonContext.Default.CameraLens, CameraLens.CreateSchema);
        game.Schemas.AddFile("material", MaterialFile.Extension, static () => RenderingJsonContext.Default.MaterialFile);
        game.Schemas.AddFile("canvas", CanvasFile.Extension, static () => RenderingJsonContext.Default.CanvasFile);
        game.Schemas.AddFile("texture", TextureSettings.Extension, static () => RenderingJsonContext.Default.TextureSettings);
        MeshSources meshes = new();
        game.Services.AddService(meshes);
        SceneRendererSetup setup = new();
        setup.Add(renderer => renderer.Meshes = meshes);
        game.Services.AddService(setup);
        ArrayRegistry.Add<Hidden>();
        ArrayRegistry.Add<ShadowOnly>();
        ArrayRegistry.Add<Greyed>();
        ArrayRegistry.Add<Undrawn>();
        ArrayRegistry.Add<Cutaway>();
        ArrayRegistry.Add<RequestedOutlines>();
        MeshAnimationSystem animation = new(game.World);
        game.FrameSystems.Add(animation);
    }

    /// <summary>
    /// Adds the mood system (a frame system and service), which keeps the mood on screen from the scene's environment and
    /// mood and the story's rules over the content given, for whatever draws the game to take each frame; after <see cref="AddRendering"/>.
    /// </summary>
    public static MoodSystem AddMoods(this Game game, IContentManager content)
    {
        ILogger<MoodSystem> logger = game.LoggerFactory.CreateLogger<MoodSystem>();
        MoodSystem moods = new(game, content, logger);
        game.FrameSystems.Add(moods);
        game.Services.AddService(moods);
        return moods;
    }
}
