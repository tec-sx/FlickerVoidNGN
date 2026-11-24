using System.Numerics;

using Arch.Core;

using BenchmarkDotNet.Attributes;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;

namespace FlickerVoid.Engine.Benchmarks.Runtime;

/// <summary>Moves nested entities with their moving parents, as the game does after each fixed step and before each frame: a tenth of the named entities are parents of nine children each.</summary>
[MemoryDiagnoser]
public class TransformHierarchyBenchmarks
{
    private const int ChildrenPerParent = 9;

    private readonly TransformHierarchy _hierarchy = new();
    private World _world = null!;
    private Entity[] _parents = [];
    private float _step;

    [Params(100, 1_000)]
    public int Entities { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = World.Create();
        int parents = Entities / (ChildrenPerParent + 1);
        _parents = new Entity[parents];
        for (int index = 0; index < parents; index++)
        {
            string name = $"Parent {index}";
            Transform placed = new() { Position = new Vector3(index, 0, 0) };
            _parents[index] = _world.Create(new EntityName(name), placed);
            for (int child = 0; child < ChildrenPerParent; child++)
            {
                Transform beside = new() { Position = new Vector3(index, child + 1, 0) };
                _world.Create(new EntityName($"{name} child {child}"), new EntityParent(name), beside);
            }
        }

        _hierarchy.Update(_world);
    }

    [GlobalCleanup]
    public void Cleanup() => World.Destroy(_world);

    [Benchmark]
    public void Update()
    {
        _step += 0.01f;
        foreach (Entity parent in _parents)
        {
            ref Transform transform = ref _world.Get<Transform>(parent);
            transform.Position = transform.Position with { Z = _step };
        }

        _hierarchy.Update(_world);
    }
}
