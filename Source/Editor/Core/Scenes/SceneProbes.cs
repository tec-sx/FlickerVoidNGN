using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>
/// Bakes every irradiance volume in the edited world into its probes file in the content folder, which the viewport then
/// reloads. A volume without a file gets one named after the scene and the volume, beside the scene file.
/// </summary>
public static class SceneProbes
{
    private const string UnsavedScene = "Untitled";
    private const string ProbesExtension = ".fvprobes";

    /// <summary>Bakes with the mood's sun, ambient light, fog and shadows, and returns the probes files written.</summary>
    public static IReadOnlyList<string> Bake(IGraphicsDevice device, EditorScene scene, LightingMood mood)
    {
        World world = scene.Game.World;
        List<Entity> volumes = [];
        QueryDescription query = new QueryDescription().WithAll<Transform, IrradianceVolume>();
        world.Query(query, (Entity entity) => volumes.Add(entity));
        if (volumes.Count == 0)
        {
            return [];
        }

        using IrradianceBaker baker = new(device, scene.Content);
        baker.Renderer.Mood = mood;
        List<string> baked = [];
        foreach (Entity entity in volumes)
        {
            IrradianceVolume volume = world.Get<IrradianceVolume>(entity);
            if (volume.Probes is null)
            {
                volume.Probes = NameProbes(scene, entity);
                scene.SetComponent(entity, volume);
            }

            Vector3 center = world.Get<Transform>(entity).Position;
            IrradianceProbes probes = baker.Bake(world, volume, center);
            string path = volume.Probes;
            Save(scene, path, probes);
            baked.Add(path);
        }

        return baked;
    }

    private static void Save(EditorScene scene, string path, IrradianceProbes probes)
    {
        using MemoryStream stream = new();
        probes.Save(stream);
        byte[] contents = stream.ToArray();
        scene.Content.Write(path, contents);
    }

    private static string NameProbes(EditorScene scene, Entity entity)
    {
        World world = scene.Game.World;
        string volumeName = $"Volume{entity.Id}";
        if (world.TryGet(entity, out EntityName name))
        {
            volumeName = name.Value;
        }

        string? scenePath = scene.ScenePath;
        if (scenePath is null)
        {
            return $"{UnsavedScene}.{volumeName}{ProbesExtension}";
        }

        // Scene files are Name.fvscene.
        string withoutJson = Path.GetFileNameWithoutExtension(scenePath);
        string sceneName = Path.GetFileNameWithoutExtension(withoutJson);
        string fileName = $"{sceneName}.{volumeName}{ProbesExtension}";
        string? sceneFolder = Path.GetDirectoryName(scenePath);
        if (string.IsNullOrEmpty(sceneFolder))
        {
            return fileName;
        }

        string folder = sceneFolder.Replace('\\', '/');
        return $"{folder}/{fileName}";
    }
}
