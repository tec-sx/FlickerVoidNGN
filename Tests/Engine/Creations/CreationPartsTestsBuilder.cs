using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Entities;
using FlickerVoid.Engine.Runtime.Scenes;

namespace FlickerVoid.Engine.Creations.Tests;

/// <summary>
/// Builds the parts under test over a new world whose spawner places the test kind, with the parts that kind describes and a record
/// of the looks its parts attached and detached.
/// </summary>
internal sealed class CreationPartsTestsBuilder : IDisposable
{
    public CreationPartsTestsBuilder()
    {
        Spawner.Register("creation", CreationsTestsJsonContext.Default.TestCreation);
        Spawner.Lifecycle.OnAttach((Entity entity, ref TestLook look) => Attached.Add(look.Mesh));
        Spawner.Lifecycle.OnDetach((Entity entity, ref TestLook look) => Detached.Add(look.Mesh));
        Parts = new CreationParts(World, Spawner.Lifecycle);
        Parts.AddKind<TestCreation>(_ => Described);
    }

    public World World { get; } = World.Create();

    public SceneSpawner Spawner { get; } = new();

    public TransformHierarchy Hierarchy { get; } = new();

    public CreationParts Parts { get; }

    /// <summary>The parts the test kind describes for every placed creation.</summary>
    public List<PartData> Described { get; } = [];

    public List<string> Attached { get; } = [];

    public List<string> Detached { get; } = [];

    public CreationPartsTestsBuilder Describe(string id, Vector3 place, string mesh)
    {
        Transform transform = new() { Position = place };
        TestLook look = new(mesh);
        PartData part = new PartData(id, transform).With(look);
        Described.Add(part);
        return this;
    }

    /// <summary>Changes what the test kind describes to these parts, as an edit of the creation's file does.</summary>
    public void Redescribe(params PartData[] parts)
    {
        Described.Clear();
        Described.AddRange(parts);
    }

    public static PartData Part(string id, Vector3 place, string mesh)
    {
        Transform transform = new() { Position = place };
        TestLook look = new(mesh);
        return new PartData(id, transform).With(look);
    }

    public Entity Place(Vector3 position, Quaternion? rotation = null)
    {
        Transform transform = new() { Position = position, Rotation = rotation ?? Quaternion.Identity };
        TestCreation creation = new() { File = "Vans/Van.fvtest" };
        Entity root = World.Create(transform, creation);
        Spawner.Lifecycle.Start(World, root);
        return root;
    }

    public void Move(Entity entity, Vector3 position, Quaternion? rotation = null)
    {
        Transform transform = World.Get<Transform>(entity);
        transform.Position = position;
        transform.Rotation = rotation ?? transform.Rotation;
        World.Set(entity, transform);
    }

    public Vector3 PositionOf(Entity entity) => World.Get<Transform>(entity).Position;

    public void Dispose() => World.Destroy(World);
}
