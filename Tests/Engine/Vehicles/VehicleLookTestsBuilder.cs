using System.Numerics;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Vehicles.Tests;

/// <summary>Builds a look as the builder bakes one from Blender: one triangle per object, in the vehicle's space, named by its nodes.</summary>
internal sealed class VehicleLookTestsBuilder
{
    private readonly List<MeshPart> _parts = [];

    public VehicleFile Vehicle { get; } = VehicleFileTestsBuilder.WithDoor();

    /// <summary>Adds a triangle with a corner at <paramref name="corner"/> in the vehicle's space, read from nodes named innermost first.</summary>
    public VehicleLookTestsBuilder WithObject(Vector3 corner, params string[] nodes)
    {
        MeshVertex[] vertices =
        [
            new(corner, Vector3.UnitZ, Vector2.Zero),
            new(corner + Vector3.UnitX, Vector3.UnitZ, Vector2.UnitX),
            new(corner + Vector3.UnitY, Vector3.UnitZ, Vector2.UnitY),
        ];
        MaterialData material = new(Vector4.One, null);
        MeshPart part = new(vertices, [0, 2, 1], material) { Nodes = nodes };
        _parts.Add(part);
        return this;
    }

    public VehicleLook Build()
    {
        ModelData look = new(_parts, []);
        return VehicleLook.Bind(Vehicle, look);
    }
}
