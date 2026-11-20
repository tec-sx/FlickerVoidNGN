using System.Numerics;

using BenchmarkDotNet.Attributes;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Benchmarks.Rendering;

/// <summary>Sorts lights scattered through a 1600x900 view into the clusters, as every frame does on the CPU.</summary>
[MemoryDiagnoser]
public class LightClustersBenchmarks
{
    private const int Seed = 7;

    private readonly LightClusters _clusters = new();
    private Camera _camera;
    private CameraView _view;
    private LightBounds[] _lights = [];

    [Params(16, 128, 512)]
    public int Lights { get; set; }

    [Params(CameraProjection.Orthographic, CameraProjection.Perspective)]
    public CameraProjection Projection { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _camera = new Camera { Projection = Projection, OrthographicHeight = 20, FarPlane = 60 };
        Transform transform = new() { Position = new Vector3(0, 0, 10) };
        _view = CameraView.Create(_camera, transform, 1600, 900);

        Random random = new(Seed);
        _lights = new LightBounds[Lights];
        for (int index = 0; index < Lights; index++)
        {
            float x = (random.NextSingle() * 36) - 18;
            float y = (random.NextSingle() * 20) - 10;
            float z = -random.NextSingle() * 40;
            float range = 1 + (random.NextSingle() * 3);
            _lights[index] = new LightBounds(new Vector3(x, y, z), range);
        }
    }

    [Benchmark]
    public void Build()
    {
        _clusters.Build(_camera, _view, _lights);
    }
}
