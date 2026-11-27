using System.Numerics;
using System.Text.Json.Nodes;

using FlickerVoid.Editor.Assets.Vehicles;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Solids;
using FlickerVoid.Engine.Vehicles;
using FlickerVoid.Tools.Builder.Models;

using SharpGLTF.Schema2;

namespace FlickerVoid.Editor.Assets.Tests.Vehicles;

public sealed class VehicleExportTests
{
    private readonly VehicleFile _car = VehicleFile.Create(VehicleType.Car);

    [Fact]
    public void ExportsAnObjectPerPartNamedByItsIdAtItsPivotUnderTheVehicle()
    {
        ModelRoot model = VehicleExport.ToGltf(_car, "Van");

        Node root = model.DefaultScene.VisualChildren.Should().ContainSingle().Subject;
        root.Name.Should().Be("Van");
        root.VisualChildren.Select(node => node.Name).Should().Equal("body", "wheel.1L", "wheel.1R", "wheel.2L", "wheel.2R");
        Node wheel = root.VisualChildren.ElementAt(1);
        wheel.LocalTransform.Translation.Should().Be(_car.Wheels[0].Place.Position);
        JsonNode? part = wheel.Extras![GltfImporter.PartProperty];
        part!.GetValue<string>().Should().Be("wheel.1L");
    }

    [Fact]
    public void WhatIsExportedComesBackAsEachPartsLookWhereItsGreyboxIs()
    {
        ModelRoot model = VehicleExport.ToGltf(_car, "Van");

        ModelData imported = GltfImporter.Import(model);
        VehicleLook look = VehicleLook.Bind(_car, imported);

        look.Unmatched.Should().BeEmpty();
        look.Greyboxed.Should().BeEmpty();
        SolidTriangles greybox = _car.Wheels[0].Mesh.Triangulate();
        MeshPart wheel = look.Parts["wheel.1L"].Parts.Should().ContainSingle().Subject;
        wheel.Indices.Should().HaveCount(greybox.Indices.Length);
        Vector3[] corners = [.. wheel.Indices.Take(3).Select(index => wheel.Vertices[index].Position)];
        Vector3[] expected = [.. greybox.Indices.Take(3).Select(index => greybox.Vertices[index].Position)];
        for (int corner = 0; corner < 3; corner++)
        {
            float off = Vector3.Distance(corners[corner], expected[corner]);
            off.Should().BeLessThan(1e-4f, "positions and winding survive the trip");
        }
    }

    [Fact]
    public void TheBytesAreAGlbBlenderCanRead()
    {
        byte[] glb = VehicleExport.ToGlb(_car, "Van");

        using MemoryStream stream = new(glb);
        ModelRoot read = ModelRoot.ReadGLB(stream);
        read.LogicalNodes.Should().HaveCount(6);
    }
}
