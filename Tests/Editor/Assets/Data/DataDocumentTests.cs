using FlickerVoid.Editor.Assets.Data;

namespace FlickerVoid.Editor.Assets.Tests.Data;

public sealed class DataDocumentTests : IDisposable
{
    private readonly DataDocumentTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ReadsTheAssetAsItsRegisteredType()
    {
        DataDocument document = _builder.Build();

        document.Asset.Should().BeOfType<Barrel>().Which.Litres.Should().Be(80);
    }

    [Fact]
    public void HoldsEditsUnsavedUntilTheyAreSaved()
    {
        DataDocument document = _builder.Build();
        Barrel barrel = (Barrel)document.Asset;

        barrel.Litres = 120;
        document.Hold();

        _builder.Content.IsUnsaved(DataDocumentTestsBuilder.Cellar).Should().BeTrue();
        _builder.ReadFile().Should().Contain("80");
        _builder.Content.Save(DataDocumentTestsBuilder.Cellar);
        string saved = _builder.ReadFile();
        saved.Should().Contain("\"litres\": 120").And.Contain("\"type\": \"barrel\"").And.Contain("\"$schema\": \"../.schemas/data.schema.json\"");
    }

    [Fact]
    public void OpensWithTheUnsavedEdits()
    {
        DataDocument first = _builder.Build();
        ((Barrel)first.Asset).Litres = 120;
        first.Hold();

        DataDocument again = _builder.Build();

        again.Asset.Should().BeOfType<Barrel>().Which.Litres.Should().Be(120);
        again.Asset.Should().NotBeSameAs(first.Asset);
    }
}
