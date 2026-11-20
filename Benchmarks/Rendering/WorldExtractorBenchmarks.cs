using System.Numerics;

using Arch.Core;

using BenchmarkDotNet.Attributes;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Benchmarks.Rendering;

/// <summary>Copies a world of meshes, animated meshes and point lights into a render scene, as every frame does before drawing.</summary>
[MemoryDiagnoser]
public class WorldExtractorBenchmarks
{
    private readonly WorldExtractor _extractor = new();
    private readonly RenderScene _scene = new();
    private World _world = null!;

    [Params(1_000, 10_000)]
    public int Entities { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = World.Create();
        _world.Create(new Camera(), new Transform());
        string[] materials = ["Paper.fvmat"];
        for (int index = 0; index < Entities; index++)
        {
            Transform transform = new() { Position = new Vector3(index % 100, 0, index / 100) };
            MeshRenderer renderer = new() { Mesh = "Room.glb", Materials = materials };
            if (index % 10 == 0)
            {
                _world.Create(renderer, transform, new MeshAnimator());
            }
            else if (index % 10 == 1)
            {
                _world.Create(transform, new PointLight());
            }
            else
            {
                _world.Create(renderer, transform);
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        World.Destroy(_world);
    }

    [Benchmark]
    public void Extract()
    {
        _extractor.Extract(_world, _scene);
    }
}
