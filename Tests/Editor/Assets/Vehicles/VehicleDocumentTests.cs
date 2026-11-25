using FlickerVoid.Editor.Assets.Vehicles;
using FlickerVoid.Editor.Core.Undo;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Assets.Tests.Vehicles;

public sealed class VehicleDocumentTests : IDisposable
{
    private readonly VehicleDocumentTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void HoldsAnEditUnsavedWhereEveryLoadSeesIt()
    {
        VehicleDocument document = _builder.Build();

        document.SetWheelCount(6);

        _builder.Content.IsUnsaved(VehicleDocumentTestsBuilder.Van).Should().BeTrue();
        _builder.Load().Wheels.Should().HaveCount(6, "placed copies load what is held");
        _builder.ReadDisk().Wheels.Should().HaveCount(4, "nothing is saved yet");
    }

    [Fact]
    public void ReportsTheFileChangedAsHotReloadDoes()
    {
        VehicleDocument document = _builder.Build();
        List<string> changed = [];
        _builder.Content.Changed += changed.Add;

        document.SetType(VehicleType.Motorcycle);

        string fullPath = _builder.Content.GetFullPath(VehicleDocumentTestsBuilder.Van);
        changed.Should().Equal(fullPath);
    }

    [Fact]
    public void UndoesEachEditAsOneStep()
    {
        VehicleDocument document = _builder.Build();
        UndoHistory history = _builder.Content.GetHistory(VehicleDocumentTestsBuilder.Van);

        document.SetWheelCount(8);
        document.SetType(VehicleType.Motorcycle);
        history.Undo();

        VehicleFile undone = _builder.Load();
        undone.Type.Should().Be(VehicleType.Car);
        undone.Wheels.Should().HaveCount(8);
        history.Undo();
        _builder.Load().Wheels.Should().HaveCount(4);
        _builder.Content.IsUnsaved(VehicleDocumentTestsBuilder.Van).Should().BeFalse("undoing every edit gives back the file on disk");
    }

    [Fact]
    public void KeepsTheSchemaTheFileNames()
    {
        VehicleDocument document = _builder.Build();

        document.SetWheelCount(6);
        _builder.Content.Save(VehicleDocumentTestsBuilder.Van);

        _builder.ReadDisk().Schema.Should().Be(".schemas/vehicle.schema.json");
    }
}
