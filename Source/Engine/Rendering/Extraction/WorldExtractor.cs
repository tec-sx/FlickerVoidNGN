using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Rendering.Outlines;
using FlickerVoid.Engine.Rendering.Sky;
using FlickerVoid.Engine.Rendering.Moods;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Greyboxes;

namespace FlickerVoid.Engine.Rendering.Extraction;

/// <summary>
/// Extracts cameras (a director's <see cref="ActiveCamera"/> first, at its posed view), meshes and greyboxes (the box, or the swap model shown in its place) with their outlines, lights,
/// indoor volumes (and the cutaway) and irradiance volumes from entities with a transform; without a director, the first camera's depth fade from the main camera.
/// </summary>
public sealed class WorldExtractor : IRenderExtractor
{
    private static readonly QueryDescription ActiveCameras = new QueryDescription().WithAll<ActiveCamera>();
    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera, Transform>().WithNone<ActiveCamera>();
    private static readonly QueryDescription Meshes = new QueryDescription().WithAll<MeshRenderer, Transform>().WithNone<MeshAnimator, Hidden, Undrawn>();
    private static readonly QueryDescription AnimatedMeshes = new QueryDescription().WithAll<MeshRenderer, Transform, MeshAnimator>().WithNone<Hidden, Undrawn>();
    private static readonly QueryDescription Greyboxes = new QueryDescription().WithAll<Greybox, Transform>().WithNone<MeshRenderer, Hidden, Undrawn>();
    private static readonly QueryDescription PointLights = new QueryDescription().WithAll<PointLight, Transform>().WithNone<Hidden, Undrawn>();
    private static readonly QueryDescription SpotLights = new QueryDescription().WithAll<SpotLight, Transform>().WithNone<Hidden, Undrawn>();
    private static readonly QueryDescription Doorways = new QueryDescription().WithAll<Doorway, Transform>().WithNone<Hidden, Undrawn, Greyed>();
    private static readonly QueryDescription IndoorVolumes = new QueryDescription().WithAll<IndoorVolume, Transform>();
    private static readonly QueryDescription Volumes = new QueryDescription().WithAll<IrradianceVolume, Transform>();
    private static readonly QueryDescription Environments = new QueryDescription().WithAll<SceneEnvironment>();

    public void Extract(World world, RenderScene scene)
    {
        scene.Clear();
        world.Query(in ActiveCameras, (ref ActiveCamera active) =>
        {
            scene.Cameras.Add(new RenderCamera(active.Lens, active.Pose));
            RenderCamera dissolveFrom = new(active.DissolveLens, active.DissolvePose);
            scene.Transition = new RenderTransition(active.Fade, active.FadeColor, active.Dissolve, dissolveFrom);
            scene.Focus = active.Focus;
            scene.Cuts = active.Cuts;
        });
        world.Query(in Environments, (ref SceneEnvironment environment) =>
        {
            if (scene.Sky is null && !string.IsNullOrEmpty(environment.SkyMask))
            {
                scene.Sky = new RenderSky(environment.SkyMask, environment.North);
            }
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
            MeshFlags flags = GetFlags(world, entity);
            scene.Meshes.Add(new RenderMesh(entity.Id, meshWorld, renderer.Mesh, null, renderer.Materials, outline, flags));
        });
        world.Query(in AnimatedMeshes, (Entity entity, ref MeshRenderer renderer, ref Transform transform, ref MeshAnimator animator) =>
        {
            if (renderer.Mesh.Length == 0)
            {
                return;
            }

            Matrix4x4 meshWorld = transform.ToMatrix();
            Outline? outline = FindOutline(world, entity);
            MeshFlags flags = GetFlags(world, entity);
            scene.Meshes.Add(new RenderMesh(entity.Id, meshWorld, renderer.Mesh, animator, renderer.Materials, outline, flags));
        });
        world.Query(in Greyboxes, (Entity entity, ref Greybox greybox, ref Transform transform) =>
        {
            Matrix4x4 meshWorld = transform.ToMatrix();
            Outline? outline = FindOutline(world, entity);
            string mesh = greybox.GetShownSwap() ?? GreyboxMesh.GetName(greybox.Size);
            MeshFlags flags = GetFlags(world, entity);
            scene.Meshes.Add(new RenderMesh(entity.Id, meshWorld, mesh, null, null, outline, flags));
        });
        world.Query(in Doorways, (Entity entity, ref Doorway doorway, ref Transform transform) =>
        {
            Matrix4x4 meshWorld = transform.ToMatrix();
            Outline outline = new() { Color = doorway.Color };
            string mesh = GreyboxMesh.GetName(doorway.Size);
            scene.Meshes.Add(new RenderMesh(entity.Id, meshWorld, mesh, null, null, outline, MeshFlags.OutlineOnly));
        });
        world.Query(in PointLights, (ref PointLight light, ref Transform transform) =>
        {
            scene.PointLights.Add(new RenderPointLight(light, transform.Position));
        });
        world.Query(in SpotLights, (ref SpotLight light, ref Transform transform) =>
        {
            scene.SpotLights.Add(new RenderSpotLight(light, transform.Position, transform.Rotation));
        });
        world.Query(in IndoorVolumes, (Entity entity, ref IndoorVolume volume, ref Transform transform) =>
        {
            (Vector3 min, Vector3 max) = volume.GetBounds(transform.Position, transform.Rotation);
            RenderIndoor indoor = new(min, max, volume.Ambient, volume.OutsideMuted);
            scene.Indoors.Add(indoor);
            if (world.Has<Cutaway>(entity))
            {
                scene.Cutaway = indoor;
            }
        });
        world.Query(in Volumes, (ref IrradianceVolume volume, ref Transform transform) =>
        {
            scene.ProbeVolumes.Add(new RenderProbeVolume(volume, transform.Position));
        });
    }

    private static MeshFlags GetFlags(World world, Entity entity)
    {
        MeshFlags flags = MeshFlags.None;
        if (world.Has<ShadowOnly>(entity))
        {
            flags |= MeshFlags.ShadowOnly;
        }

        if (world.Has<Uncut>(entity))
        {
            flags |= MeshFlags.Uncut;
        }

        if (world.Has<Greyed>(entity))
        {
            flags |= MeshFlags.Greyed;
        }

        return flags;
    }

    /// <summary>The entity's outline; none on a greyed entity, as the creator's greyed level shows no markers.</summary>
    private static Outline? FindOutline(World world, Entity entity)
    {
        if (!world.Has<Greyed>(entity) && world.TryGet(entity, out Outline outline))
        {
            return outline;
        }

        return null;
    }
}
