using System.Globalization;
using System.Text;
using System.Text.Json;

using Arch.Core;

using BenchmarkDotNet.Attributes;

using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Benchmarks.Runtime;

/// <summary>Reads, spawns and captures a scene of meshes and point lights, as loading a scene and saving it in the editor do.</summary>
[MemoryDiagnoser]
public class SceneBenchmarks
{
    private Game _game = null!;
    private byte[] _file = [];
    private SceneData _scene = null!;
    private World _spawned = null!;
    private IReadOnlyList<Entity> _entities = [];

    [Params(100, 1_000)]
    public int Entities { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _game = new Game(NullLoggerFactory.Instance);
        _game.AddRendering();
        string text = WriteScene(Entities);
        _file = Encoding.UTF8.GetBytes(text);
        _scene = Read();
        _spawned = World.Create();
        _entities = _game.Spawner.Spawn(_scene, _spawned);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        World.Destroy(_spawned);
        _game.Dispose();
    }

    [Benchmark]
    public SceneData Read()
    {
        SceneData? scene = JsonSerializer.Deserialize(_file, RuntimeJsonContext.Default.SceneData);
        return scene!;
    }

    [Benchmark]
    public int Spawn()
    {
        World world = World.Create();
        IReadOnlyList<Entity> entities = _game.Spawner.Spawn(_scene, world);
        World.Destroy(world);
        return entities.Count;
    }

    [Benchmark]
    public SceneData Capture()
    {
        return _game.Spawner.Capture(_scene, _entities, _spawned);
    }

    private static string WriteScene(int entities)
    {
        StringBuilder text = new();
        text.Append("{\"entities\": [");
        for (int index = 0; index < entities; index++)
        {
            if (index > 0)
            {
                text.Append(',');
            }

            string x = (index % 100).ToString(CultureInfo.InvariantCulture);
            string z = (index / 100).ToString(CultureInfo.InvariantCulture);
            text.Append(CultureInfo.InvariantCulture, $"{{\"name\": \"Entity {index}\", \"components\": {{\"transform\": {{\"position\": [{x}, 0, {z}]}}");
            if (index % 10 == 1)
            {
                text.Append(", \"pointLight\": {\"color\": [1, 0.8, 0.6], \"intensity\": 1.6, \"range\": 1.8}");
            }
            else
            {
                text.Append(", \"meshRenderer\": {\"mesh\": \"Room.glb\", \"materials\": [\"Paper.fvmat\"]}");
            }

            text.Append("}}");
        }

        text.Append("]}");
        return text.ToString();
    }
}
