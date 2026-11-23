using FlickerVoid.Engine.Cameras.Cutaways;
using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Cameras.Layers;
using FlickerVoid.Engine.Cameras.Scripting;
using FlickerVoid.Engine.Cameras.Serialization;
using FlickerVoid.Engine.Cameras.Transitions;
using FlickerVoid.Engine.Cameras.Triggers;
using FlickerVoid.Engine.Flow.Graphs;
using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Outlines;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Cameras;

/// <summary>
/// Adds directed cameras to a game: the <c>shot</c>, <c>cameraTarget</c>, <c>triggerArea</c>, <c>triggerActivator</c> and
/// <c>cameraCue</c> components, the trigger area system (fixed) and cue system, the <c>cutToShot</c>, <c>playShot</c> and
/// <c>shakeCamera</c> effects, the <c>switchShot</c>, <c>playShot</c>, <c>waitForShotMarker</c>, <c>fade</c> and
/// <c>shakeCamera</c> flow nodes, the <c>cameraRig</c> data asset type and its layers (<c>follow</c>, <c>look</c>, <c>shake</c>, <c>zoomPunch</c>), the camera director (a frame system and service), the cutaway system (a frame system and service) and, in every scene renderer the host creates, the transition pass; after <c>AddRendering</c>.
/// </summary>
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
        game.Data.RegisterDerived<Effect, CutToShotEffect>("cutToShot");
        game.Data.RegisterDerived<Effect, PlayShotEffect>("playShot");
        game.Data.RegisterDerived<Effect, ShakeCameraEffect>("shakeCamera");
        game.Data.RegisterDerived<FlowNode, SwitchShotNode>("switchShot");
        game.Data.RegisterDerived<FlowNode, PlayShotNode>("playShot");
        game.Data.RegisterDerived<FlowNode, WaitForShotMarkerNode>("waitForShotMarker");
        game.Data.RegisterDerived<FlowNode, FadeNode>("fade");
        game.Data.RegisterDerived<FlowNode, ShakeCameraNode>("shakeCamera");
        game.Spawner.Register("triggerArea", CamerasJsonContext.Default.TriggerArea);
        game.Spawner.Register("triggerActivator", CamerasJsonContext.Default.TriggerActivator);
        game.Spawner.Register("cameraCue", CamerasJsonContext.Default.CameraCue);
        TriggerAreaSystem triggers = new(game.World, game.Events);
        game.FixedSystems.Add(triggers);
        game.Services.AddService(triggers);
        CameraDirector director = new(game);
        game.FrameSystems.Add(director);
        game.Services.AddService(director);
        CameraCueSystem cues = new(game, director);
        game.Services.AddService(cues);
        CutawaySystem cutaways = new(game.World, director);
        game.FrameSystems.Add(cutaways);
        game.Services.AddService(cutaways);
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
