using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>Extracts cameras, meshes, lights and irradiance volumes from entities with a transform.</summary>
public sealed class WorldExtractor : IRenderExtractor
{
    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera, Transform>();
    private static readonly QueryDescription Meshes = new QueryDescription().WithAll<MeshRenderer, Transform>().WithNone<MeshAnimator>();
    private static readonly QueryDescription AnimatedMeshes = new QueryDescription().WithAll<MeshRenderer, Transform, MeshAnimator>();
    private static readonly QueryDescription PointLights = new QueryDescription().WithAll<PointLight, Transform>();
    private static readonly QueryDescription SpotLights = new QueryDescription().WithAll<SpotLight, Transform>();
    private static readonly QueryDescription Volumes = new QueryDescription().WithAll<IrradianceVolume, Transform>();

    public void Extract(World world, RenderScene scene)
    {
        scene.Clear();
        world.Query(in Cameras, (ref Camera camera, ref Transform transform) =>
        {
            scene.Cameras.Add(new RenderCamera(camera, transform));
        });
        world.Query(in Meshes, (Entity entity, ref MeshRenderer renderer, ref Transform transform) =>
        {
            Matrix4x4 meshWorld = transform.ToMatrix();
            scene.Meshes.Add(new RenderMesh(entity.Id, meshWorld, renderer.Mesh, null, renderer.Materials));
        });
        world.Query(in AnimatedMeshes, (Entity entity, ref MeshRenderer renderer, ref Transform transform, ref MeshAnimator animator) =>
        {
            Matrix4x4 meshWorld = transform.ToMatrix();
            scene.Meshes.Add(new RenderMesh(entity.Id, meshWorld, renderer.Mesh, animator, renderer.Materials));
        });
        world.Query(in PointLights, (ref PointLight light, ref Transform transform) =>
        {
            scene.PointLights.Add(new RenderPointLight(light, transform.Position));
        });
        world.Query(in SpotLights, (ref SpotLight light, ref Transform transform) =>
        {
            scene.SpotLights.Add(new RenderSpotLight(light, transform.Position, transform.Rotation));
        });
        world.Query(in Volumes, (ref IrradianceVolume volume, ref Transform transform) =>
        {
            scene.ProbeVolumes.Add(new RenderProbeVolume(volume, transform.Position));
        });
    }
}
