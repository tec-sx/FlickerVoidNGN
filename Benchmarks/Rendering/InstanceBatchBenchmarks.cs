using System.Numerics;

using Arch.Core;

using BenchmarkDotNet.Attributes;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Instancing;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Benchmarks.Rendering;

/// <summary>
/// Copies a field of plants into a render scene, as every frame does before drawing: each plant its own mesh entity, or all of
/// them copies in chunks of one instance batch each, as a scatter or a brush writes them.
/// </summary>
[MemoryDiagnoser]
public class InstanceBatchBenchmarks
{
    private const int ChunkSize = 1_000;

    private readonly WorldExtractor _extractor = new();
    private readonly RenderScene _scene = new();
    private World _meshes = null!;
    private World _batches = null!;

    [Params(10_000)]
    public int Plants { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _meshes = World.Create();
        _batches = World.Create();
        _meshes.Create(new Camera(), new Transform());
        _batches.Create(new Camera(), new Transform());
        MeshRenderer renderer = new() { Mesh = "Bush.glb" };
        for (int index = 0; index < Plants; index++)
        {
            Transform transform = new() { Position = Place(index) };
            _meshes.Create(renderer, transform);
        }

        for (int first = 0; first < Plants; first += ChunkSize)
        {
            InstanceBatch batch = new("Bush.glb");
            int count = Math.Min(ChunkSize, Plants - first);
            Span<Instance> copies = batch.Rewrite(count);
            for (int index = 0; index < count; index++)
            {
                copies[index] = new Instance(Place(first + index), index, 0.5f, 0, 0);
            }

            _batches.Create(new InstancedMesh(batch));
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        World.Destroy(_meshes);
        World.Destroy(_batches);
    }

    [Benchmark(Baseline = true)]
    public void ExtractMeshes()
    {
        _extractor.Extract(_meshes, _scene);
    }

    [Benchmark]
    public void ExtractBatches()
    {
        _extractor.Extract(_batches, _scene);
    }

    private static Vector3 Place(int index)
    {
        return new Vector3(index % 100, 0, index / 100);
    }
}
