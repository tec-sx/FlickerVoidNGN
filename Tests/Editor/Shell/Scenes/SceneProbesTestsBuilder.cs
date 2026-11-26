using System.Numerics;

using Arch.Core;

using FlickerVoid.Editor.Assets.Materials;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Editor.Core.Tests.Scenes;
using FlickerVoid.Editor.Materials;
using FlickerVoid.Editor.Scenes;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;

using FlickerVoid.Engine.Runtime.Content;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Tests.Scenes;

/// <summary>
/// Builds an unsaved scene in a temporary content folder and bakes its probes on the test machine's GPU, skipping the test without
/// one or where it has no ray queries.
/// </summary>
internal sealed class SceneProbesTestsBuilder : IDisposable
{
    public const int ProbesPerAxis = 2;

    private readonly string _contentFolder;
    private readonly EditorScene _scene;

    public SceneProbesTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
        _scene = new EditorScene(NullLoggerFactory.Instance, new ContentManager(_contentFolder));
    }

    public EditorScene Scene => _scene;

    private World World => _scene.Game.World;

    /// <summary>A black cube glowing green in the middle of the volume.</summary>
    public SceneProbesTestsBuilder WithGreenGlowingCube()
    {
        string modelPath = Path.Combine(_contentFolder, "Cube.fvmodel");
        ModelData cube = PreviewMeshes.CreateCube(size: 1);
        using (FileStream stream = File.Create(modelPath))
        {
            ModelFile.Write(cube, stream);
        }

        string materialPath = Path.Combine(_contentFolder, "Glow.fvmat");
        File.WriteAllText(materialPath, """{ "parameters": { "tint": [0, 0, 0], "emissive": [0, 1, 0], "emissiveIntensity": 4 } }""");
        MeshRenderer renderer = new() { Mesh = "Cube.glb", Materials = ["Glow.fvmat"] };
        World.Create(new Transform(), renderer);
        return this;
    }

    public SceneProbesTestsBuilder WithVolume(string name, string? probes)
    {
        IrradianceVolume volume = new() { Size = new Vector3(4), ProbesX = ProbesPerAxis, ProbesY = ProbesPerAxis, ProbesZ = ProbesPerAxis, Probes = probes };
        World.Create(new Transform(), volume, new EntityName(name));
        return this;
    }

    /// <summary>Bakes every volume, skipping the test where the GPU has no ray queries to bake with.</summary>
    public IReadOnlyList<string> Bake()
    {
        using VulkanGraphicsDevice device = CreateDevice();
        LightingMood mood = new() { Ambient = 0, SunShadowDistance = 0 };
        IReadOnlyList<string> baked = [];
        try
        {
            baked = SceneProbes.Bake(device, _scene, mood);
        }
        catch (GraphicsUnavailableException exception)
        {
            Assert.Skip(exception.Message);
        }

        return baked;
    }

    public IrradianceProbes LoadProbes(string path)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        using FileStream stream = File.OpenRead(fullPath);
        return IrradianceProbes.Load(stream);
    }

    /// <summary>The probes file the named volume points at.</summary>
    public string? VolumeProbes(string name)
    {
        string? probes = null;
        QueryDescription query = new QueryDescription().WithAll<IrradianceVolume, EntityName>();
        World.Query(query, (ref IrradianceVolume volume, ref EntityName entityName) =>
        {
            if (entityName.Value == name)
            {
                probes = volume.Probes;
            }
        });
        return probes;
    }

    public void Dispose()
    {
        _scene.Dispose();
        Directory.Delete(_contentFolder, recursive: true);
    }

    private static VulkanGraphicsDevice CreateDevice()
    {
        VulkanGraphicsDevice? device = null;
        try
        {
            device = new VulkanGraphicsDevice(new GraphicsDeviceOptions(), NullLoggerFactory.Instance);
        }
        catch (GraphicsUnavailableException exception)
        {
            Assert.Skip(exception.Message);
        }

        return device;
    }
}
