using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Outlines;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>
/// Extracts cameras (a director's <see cref="ActiveCamera"/> first, at its posed view), meshes with their outlines, lights and
/// irradiance volumes from entities with a transform.
/// </summary>
public sealed class WorldExtractor : IRenderExtractor
{
    private static readonly QueryDescription ActiveCameras = new QueryDescription().WithAll<ActiveCamera>();
    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera, Transform>().WithNone<ActiveCamera>();
    private static readonly QueryDescription Meshes = new QueryDescription().WithAll<MeshRenderer, Transform>().WithNone<MeshAnimator>();
    private static readonly QueryDescription AnimatedMeshes = new QueryDescription().WithAll<MeshRenderer, Transform, MeshAnimator>();
    private static readonly QueryDescription PointLights = new QueryDescription().WithAll<PointLight, Transform>();
    private static readonly QueryDescription SpotLights = new QueryDescription().WithAll<SpotLight, Transform>();
    private static readonly QueryDescription Volumes = new QueryDescription().WithAll<IrradianceVolume, Transform>();

    public void Extract(World world, RenderScene scene)
    {
        scene.Clear();
        world.Query(in ActiveCameras, (ref ActiveCamera active) =>
        {
            scene.Cameras.Add(new RenderCamera(active.Lens, active.Pose));
        });
        world.Query(in Cameras, (ref Camera camera, ref Transform transform) =>
        {
            scene.Cameras.Add(new RenderCamera(camera, transform));
        });
        world.Query(in Meshes, (Entity entity, ref MeshRenderer renderer, ref Transform transform) =>
        {
            if (renderer.Mesh.Length == 0)
            {
                return;
            }

            Matrix4x4 meshWorld = transform.ToMatrix();
            Outline? outline = FindOutline(world, entity);
            scene.Meshes.Add(new RenderMesh(entity.Id, meshWorld, renderer.Mesh, null, renderer.Materials, outline));
        });
        world.Query(in AnimatedMeshes, (Entity entity, ref MeshRenderer renderer, ref Transform transform, ref MeshAnimator animator) =>
        {
            if (renderer.Mesh.Length == 0)
            {
                return;
            }

            Matrix4x4 meshWorld = transform.ToMatrix();
            Outline? outline = FindOutline(world, entity);
            scene.Meshes.Add(new RenderMesh(entity.Id, meshWorld, renderer.Mesh, animator, renderer.Materials, outline));
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

    private static Outline? FindOutline(World world, Entity entity)
    {
        if (world.TryGet(entity, out Outline outline))
        {
            return outline;
        }

        return null;
    }
}
