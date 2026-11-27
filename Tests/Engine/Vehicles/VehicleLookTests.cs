using System.Numerics;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Rendering.Meshes;

namespace FlickerVoid.Engine.Vehicles.Tests;

public sealed class VehicleLookTests
{
    private readonly VehicleLookTestsBuilder _builder = new();

    [Fact]
    public void AnObjectNamedByAPartIdIsThatPartsLook()
    {
        VehicleLook look = _builder.WithObject(Vector3.Zero, "wheel.1L").Build();

        look.Parts.Should().ContainKey("wheel.1L").WhoseValue.Parts.Should().ContainSingle();
        look.Unmatched.Should().BeEmpty();
    }

    [Fact]
    public void BlendersNumberSuffixesAreIgnored()
    {
        VehicleLook look = _builder.WithObject(Vector3.Zero, "door.1.001").Build();

        look.Parts.Should().ContainKey("door.1");
    }

    [Fact]
    public void ThePartPropertyNamesThePartWhateverTheObjectIsCalled()
    {
        // The builder puts a node's fv_part property in its name's place, so a renamed object keeps its part.
        VehicleLook look = _builder.WithObject(Vector3.Zero, "body", "Van").Build();

        look.Parts.Should().ContainKey("body");
    }

    [Fact]
    public void AnObjectUnderAPartBelongsToIt()
    {
        VehicleLook look = _builder.WithObject(Vector3.Zero, "Handle", "Panel", "door.1").WithObject(Vector3.Zero, "Glass", "door.1").Build();

        look.Parts["door.1"].Parts.Should().HaveCount(2);
    }

    [Fact]
    public void WhatMatchesNoPartJoinsTheBodyAndIsReported()
    {
        VehicleLook look = _builder.WithObject(Vector3.Zero, "Mirror.002", "Extras").Build();

        look.Parts.Should().ContainKey(VehicleFile.BodyId);
        look.Unmatched.Should().Equal("Mirror.002");
    }

    [Fact]
    public void APartWithNoObjectKeepsItsGreyboxAndIsReported()
    {
        VehicleLook look = _builder.WithObject(Vector3.Zero, "body").Build();

        look.Parts.Should().NotContainKey("door.1");
        look.Greyboxed.Should().Equal("wheel.1L", "wheel.1R", "wheel.2L", "wheel.2R", "door.1");
    }

    [Fact]
    public void EachPartsLookIsInItsPivotsSpaceWhereverBlenderPutTheOrigin()
    {
        SolidPart wheel = _builder.Vehicle.Wheels[0];
        Vector3 pivot = wheel.Place.Position;

        VehicleLook look = _builder.WithObject(pivot + Vector3.UnitY, "wheel.1L").Build();

        MeshPart part = look.Parts["wheel.1L"].Parts[0];
        float off = Vector3.Distance(part.Vertices[0].Position, Vector3.UnitY);
        off.Should().BeLessThan(1e-5f);
        part.Indices.Should().Equal(0, 2, 1);
    }

    [Fact]
    public void ATurnedPivotTurnsTheLookBackIntoItsSpace()
    {
        SolidPart door = _builder.Vehicle.Doors[0];
        door.Place = door.Place with { Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2) };
        Vector3 inVehicle = door.Place.Position + Vector3.Transform(Vector3.UnitX, door.Place.Rotation);

        VehicleLook look = _builder.WithObject(inVehicle, "door.1").Build();

        Vector3 position = look.Parts["door.1"].Parts[0].Vertices[0].Position;
        float off = Vector3.Distance(position, Vector3.UnitX);
        off.Should().BeLessThan(1e-5f);
    }
}
