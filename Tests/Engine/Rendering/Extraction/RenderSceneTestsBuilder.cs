using System.Numerics;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Tests.Extraction;

/// <summary>Builds render scenes with something in every list and property.</summary>
internal sealed class RenderSceneTestsBuilder
{
    public RenderScene BuildFull()
    {
        RenderScene scene = new();
        Transform cameraTransform = new() { Position = new Vector3(1, 2, 3) };
        RenderCamera camera = new(new Camera { OrthographicHeight = 6 }, cameraTransform);
        scene.Cameras.Add(camera);
        scene.Meshes.Add(new RenderMesh(4, Matrix4x4.CreateTranslation(1, 0, 0), "Room.glb", null));
        scene.PointLights.Add(new RenderPointLight(new PointLight { Intensity = 2 }, new Vector3(0, 2, 0)));
        scene.SpotLights.Add(new RenderSpotLight(new SpotLight { Intensity = 3 }, new Vector3(0, 3, 0), Quaternion.Identity));
        scene.ProbeVolumes.Add(new RenderProbeVolume(new IrradianceVolume(), new Vector3(0, 1, 0)));
        RenderIndoor indoor = new(new Vector3(-2, 0, -2), new Vector3(2, 3, 2), 0.2f, 0.5f);
        scene.Indoors.Add(indoor);
        scene.Cutaway = indoor;
        scene.Transition = new RenderTransition(0.5f, Vector3.One, 0.25f, camera);
        scene.Focus = new Vector3(0, 1, 0);
        return scene;
    }
}
