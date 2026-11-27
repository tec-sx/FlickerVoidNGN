using System.Numerics;

using FlickerVoid.Editor.Assets.Vehicles;
using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Assets.Tests.Vehicles;

/// <summary>Builds the creator's panel over a vehicle document of a new car.</summary>
internal sealed class VehiclePanelViewModelTestsBuilder : IDisposable
{
    private readonly VehicleDocumentTestsBuilder _document = new();

    public UnsavedContent Content => _document.Content;

    public IVehicleLookTools Looks { get; } = Substitute.For<IVehicleLookTools>();

    public VehiclePanelViewModel Build()
    {
        VehicleDocument document = _document.Build();
        return new VehiclePanelViewModel(document);
    }

    public VehiclePanelViewModel BuildWithLooks()
    {
        VehicleDocument document = _document.Build();
        return new VehiclePanelViewModel(document, Looks);
    }

    /// <summary>Gives the car a look with an object per named node list, as the content build would bake it.</summary>
    public void WithLook(params string[][] objects)
    {
        List<MeshPart> parts = [];
        foreach (string[] nodes in objects)
        {
            MeshVertex[] vertices = [new(Vector3.Zero, Vector3.UnitZ, Vector2.Zero), new(Vector3.UnitX, Vector3.UnitZ, Vector2.UnitX), new(Vector3.UnitY, Vector3.UnitZ, Vector2.UnitY)];
            MaterialData material = new(Vector4.One, null);
            parts.Add(new MeshPart(vertices, [0, 2, 1], material) { Nodes = nodes });
        }

        VehicleFile car = VehicleFile.Create(VehicleType.Car);
        ModelData model = new(parts, []);
        VehicleLook look = VehicleLook.Bind(car, model);
        Looks.FindLook(VehicleDocumentTestsBuilder.Van).Returns(look);
    }

    public VehicleFile Load() => _document.Load();

    public void Dispose() => _document.Dispose();
}
