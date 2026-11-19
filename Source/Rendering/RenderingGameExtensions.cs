using FlickerVoid.Engine.Runtime;

namespace FlickerVoid.Engine.Rendering;

/// <summary>Adds rendering to a game.</summary>
public static class RenderingGameExtensions
{
    /// <summary>Lets scene files add "camera", "meshRenderer", "meshAnimator", "pointLight", "spotLight" and "irradianceVolume" components, which a <see cref="SceneRenderer"/> draws, and a <see cref="MeshAnimationSystem"/> to the frame systems.</summary>
    public static void AddRendering(this Game game)
    {
        game.Spawner.Register("camera", RenderingJsonContext.Default.Camera);
        game.Spawner.Register("meshRenderer", RenderingJsonContext.Default.MeshRenderer);
        game.Spawner.Register("meshAnimator", RenderingJsonContext.Default.MeshAnimator);
        game.Spawner.Register("pointLight", RenderingJsonContext.Default.PointLight);
        game.Spawner.Register("spotLight", RenderingJsonContext.Default.SpotLight);
        game.Spawner.Register("irradianceVolume", RenderingJsonContext.Default.IrradianceVolume);
        MeshAnimationSystem animation = new(game.World);
        game.FrameSystems.Add(animation);
    }
}
