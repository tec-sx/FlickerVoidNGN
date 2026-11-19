using System.Diagnostics;
using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.GlobalIllumination;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Samples.SceneViewer;

/// <summary>Fits an irradiance volume to a model's bounds, bakes it with the renderer settings the viewer uses and saves the probes beside the model.</summary>
public static class ModelProbes
{
    // About one probe per this many world units, two to eight along each axis.
    private const float ProbeSpacing = 1.5f;

    public static void Bake(IGraphicsDevice device, IContentManager content, World world, string modelPath, LightingMood mood)
    {
        // The content build baked the model next to its source name.
        string bakedPath = Path.ChangeExtension(modelPath, ".model");
        ModelData model;
        using (FileStream stream = File.OpenRead(bakedPath))
        {
            model = ModelFile.Read(stream);
        }

        IReadOnlyList<MeshPart> parts = model.Parts;
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        foreach (MeshPart part in parts)
        {
            foreach (MeshVertex vertex in part.Vertices)
            {
                min = Vector3.Min(min, vertex.Position);
                max = Vector3.Max(max, vertex.Position);
            }
        }

        Vector3 size = max - min;
        Vector3 center = (min + max) / 2;
        string probesName = Path.GetFileNameWithoutExtension(modelPath) + ".probes";
        IrradianceVolume volume = new()
        {
            Size = size,
            ProbesX = CountProbes(size.X),
            ProbesY = CountProbes(size.Y),
            ProbesZ = CountProbes(size.Z),
            Probes = probesName,
        };

        Stopwatch watch = Stopwatch.StartNew();
        using IrradianceBaker baker = new(device, content);
        baker.Renderer.Mood = mood;

        IrradianceProbes probes = baker.Bake(world, volume, center);
        string probesPath = content.GetFullPath(probesName);
        using (FileStream stream = File.Create(probesPath))
        {
            probes.Save(stream);
        }

        Console.WriteLine($"Baked {volume.ProbesX}x{volume.ProbesY}x{volume.ProbesZ} probes into {probesPath} in {watch.Elapsed.TotalSeconds:0.0} s.");
        Transform transform = new() { Position = center };
        world.Create(transform, volume);
    }

    private static int CountProbes(float size)
    {
        int count = (int)MathF.Round(size / ProbeSpacing);
        return Math.Clamp(count, 2, 8);
    }
}
