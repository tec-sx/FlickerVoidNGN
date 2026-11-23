
using System.Text.Json.Nodes;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering;

/// <summary>Adds rendering to a game.</summary>
public static class RenderingGameExtensions
{
    /// <summary>Lets scene files add "camera", "meshRenderer", "meshAnimator", "pointLight", "spotLight", "irradianceVolume", "sceneMood", "outline", "mainCamera", "depthFadeOverride" and "indoorVolume" components, which a <see cref="SceneRenderer"/> draws, a <see cref="MeshAnimationSystem"/> to the frame systems, the <see cref="SceneRendererSetup"/> service, and the schemas of lighting mood and material files.</summary>
    public static void AddRendering(this Game game)
    {
        game.Spawner.Register("camera", RenderingJsonContext.Default.Camera);
        game.Spawner.Register("meshRenderer", RenderingJsonContext.Default.MeshRenderer);
        game.Spawner.Register("meshAnimator", RenderingJsonContext.Default.MeshAnimator);
        game.Spawner.Register("pointLight", RenderingJsonContext.Default.PointLight);
        game.Spawner.Register("spotLight", RenderingJsonContext.Default.SpotLight);
        game.Spawner.Register("irradianceVolume", RenderingJsonContext.Default.IrradianceVolume);
        game.Spawner.Register("sceneMood", RenderingJsonContext.Default.SceneMood);
        game.Spawner.Register("outline", RenderingJsonContext.Default.Outline);
        game.Spawner.Register("mainCamera", RenderingJsonContext.Default.MainCamera);
        game.Spawner.Register("depthFadeOverride", RenderingJsonContext.Default.DepthFadeOverride);
        game.Spawner.Register("indoorVolume", RenderingJsonContext.Default.IndoorVolume);
        game.Schemas.Add("mood", CreateMoodSchema);
        game.Schemas.Add("material", CreateMaterialSchema);
        game.Schemas.Add("canvas", CreateCanvasSchema);
        game.Schemas.Add("texture", CreateTextureSchema);
        game.Services.AddService(new SceneRendererSetup());
        ArrayRegistry.Add<Hidden>();
        ArrayRegistry.Add<ShadowOnly>();
        ArrayRegistry.Add<Cutaway>();
        MeshAnimationSystem animation = new(game.World);
        game.FrameSystems.Add(animation);
    }

    private static JsonObject CreateMoodSchema() => JsonSchemas.CreateFile(RenderingJsonContext.Default.LightingMood);

    private static JsonObject CreateCanvasSchema() => JsonSchemas.CreateFile(RenderingJsonContext.Default.CanvasFile);

    private static JsonObject CreateTextureSchema() => JsonSchemas.CreateFile(RenderingJsonContext.Default.TextureSettings);

    private static JsonObject CreateMaterialSchema() => JsonSchemas.CreateFile(RenderingJsonContext.Default.MaterialFile);
}
