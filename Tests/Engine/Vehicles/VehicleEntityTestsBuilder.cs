using System.Numerics;
using System.Text;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;
using FlickerVoid.Engine.Vehicles.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Vehicles.Tests;

/// <summary>Builds a game with rendering and vehicles over a temporary content folder, without a window.</summary>
internal sealed class VehicleEntityTestsBuilder : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FlickerVoidVehicleTests", Guid.NewGuid().ToString("N"));

    public VehicleEntityTestsBuilder()
    {
        Directory.CreateDirectory(_folder);
        Content = new ContentManager(_folder);
        Game.Services.AddService<IContentManager>(Content);
        Game.AddRendering();
    }

    public Game Game { get; } = new(NullLoggerFactory.Instance);

    public ContentManager Content { get; }

    public CreationParts Parts => Game.Services.GetService<CreationParts>()!;

    public VehicleEntityTestsBuilder WithVehicle(string path, VehicleFile vehicle)
    {
        Write(path, vehicle);
        return this;
    }

    public VehicleEntityTestsBuilder Build()
    {
        Game.AddVehicles();
        return this;
    }

    /// <summary>Spawns a scene entity named Van with a vehicle naming the file.</summary>
    public Entity Place(string file)
    {
        string json = $$"""
            { "entities": [ { "name": "Van", "components": { "transform": {}, "vehicle": { "file": "{{file}}" } } } ] }
            """;
        SceneData scene = JsonSerializer.Deserialize(json, RuntimeJsonContext.Default.SceneData)!;
        IReadOnlyList<Entity> entities = Game.Spawner.Spawn(scene, Game.World);
        return entities[0];
    }

    /// <summary>Saves the file anew, as the editor does, which reports it changed.</summary>
    public void Write(string path, VehicleFile vehicle)
    {
        string json = JsonSerializer.Serialize(vehicle, VehiclesJsonContext.Default.VehicleFile);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Content.Write(path, bytes);
    }

    /// <summary>Writes the vehicle's look as the content build bakes it beside the file: a triangle per object, named by its nodes.</summary>
    public void WriteLook(string vehiclePath, params string[][] objects)
    {
        List<MeshPart> parts = [];
        foreach (string[] nodes in objects)
        {
            MeshVertex[] vertices = [new(Vector3.Zero, Vector3.UnitZ, Vector2.Zero), new(Vector3.UnitX, Vector3.UnitZ, Vector2.UnitX), new(Vector3.UnitY, Vector3.UnitZ, Vector2.UnitY)];
            MaterialData material = new(Vector4.One, null);
            MeshPart part = new(vertices, [0, 2, 1], material) { Nodes = nodes };
            parts.Add(part);
        }

        ModelData look = new(parts, []);
        using MemoryStream stream = new();
        ModelFile.Write(look, stream);
        string path = VehicleLook.GetBuiltPath(vehiclePath);
        Content.Write(path, stream.ToArray());
    }

    public void WriteText(string path, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        Content.Write(path, bytes);
    }

    public void Dispose()
    {
        Game.Dispose();
        Directory.Delete(_folder, recursive: true);
    }
}
