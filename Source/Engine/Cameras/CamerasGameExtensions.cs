using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Cameras.Layers;
using FlickerVoid.Engine.Cameras.Serialization;
using FlickerVoid.Engine.Cameras.Transitions;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Outlines;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Cameras;

/// <summary>Adds directed cameras to a game: the <c>shot</c> and <c>cameraTarget</c> components, the <c>cameraRig</c> data asset type and its layers (<c>follow</c>, <c>look</c>, <c>shake</c>, <c>zoomPunch</c>), the camera director (a frame system and service) and, in every scene renderer the host creates, the transition pass; after <c>AddRendering</c>.</summary>
public static class CamerasGameExtensions
{
    public static CameraDirector AddCameras(this Game game)
    {
        game.Spawner.Register("shot", CamerasJsonContext.Default.Shot);
        game.Spawner.Register("cameraTarget", CamerasJsonContext.Default.CameraTarget);
        game.Data.AddResolver(CamerasJsonContext.Default);
        game.Data.Register<CameraRig>("cameraRig");
        game.Data.RegisterDerived<CameraLayer, FollowLayer>("follow");
        game.Data.RegisterDerived<CameraLayer, LookLayer>("look");
        game.Data.RegisterDerived<CameraLayer, ShakeLayer>("shake");
        game.Data.RegisterDerived<CameraLayer, ZoomPunchLayer>("zoomPunch");
        CameraDirector director = new(game);
        game.FrameSystems.Add(director);
        game.Services.AddService(director);
        SceneRendererSetup? renderers = game.Services.GetService<SceneRendererSetup>();
        renderers?.Add(AddTransitionPass);
        return director;
    }

    /// <summary>Lays transitions over the frame after the outlines, so they fade too.</summary>
    private static void AddTransitionPass(SceneRenderer renderer)
    {
        TransitionPass pass = new(renderer);
        OutlinePass? outlines = renderer.Pipeline.Find<OutlinePass>();
        if (outlines is null)
        {
            renderer.Pipeline.Add(pass);
        }
        else
        {
            renderer.Pipeline.InsertAfter(outlines, pass);
        }
    }
}
