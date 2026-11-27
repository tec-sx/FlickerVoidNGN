using System.Text.Json.Nodes;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Data;

namespace FlickerVoid.Engine.Runtime.Tests.Data;

public sealed class DataAssetsTests : IDisposable
{
    private const string Key = """
        {
          "$schema": "../.schemas/data.schema.json",
          "type": "item",
          "id": "Item.Key.Cellar",
          "display": { "name": { "key": "Test.CellarKey", "text": "Cellar key" } },
          "fragments": [
            { "$type": "pickup", "mesh": "Props/Key.glb" },
            { "$type": "usable", "uses": 3 }
          ]
        }
        """;

    private readonly DataAssetsTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void LoadsADataAssetWithItsFragments()
    {
        DataAssets data = _builder.WithFile("Items/Key.fvdata", Key).Build();

        TestItem key = data.Load<TestItem>("Items/Key.fvdata");

        key.Id.Should().Be(new Tag("Item.Key.Cellar"));
        key.Display.Name.Text.Should().Be("Cellar key");
        key.Fragments.Should().HaveCount(2);
        key.Fragments[0].Should().BeOfType<PickupFragment>().Which.Mesh.Should().Be("Props/Key.glb");
        key.Fragments[1].Should().BeOfType<UsableFragment>().Which.Uses.Should().Be(3);
    }

    [Fact]
    public void LoadsAnAssetAsABaseOfItsType()
    {
        DataAssets data = _builder.WithFile("Items/Key.fvdata", Key).Build();

        object key = data.Load<object>("Items/Key.fvdata");

        key.Should().BeOfType<TestItem>();
    }

    [Fact]
    public void RefusesAFileOfAnotherType()
    {
        DataAssets data = _builder.WithFile("Items/Key.fvdata", Key).Build();

        Action load = () => data.Load<TestFaction>("Items/Key.fvdata");

        load.Should().Throw<InvalidDataException>().WithMessage("*Items/Key.fvdata*item*");
    }

    [Fact]
    public void RefusesAnUnknownType()
    {
        DataAssets data = _builder.WithFile("Items/Key.fvdata", """{ "type": "weapon" }""").Build();

        Action load = () => data.Load<TestItem>("Items/Key.fvdata");

        load.Should().Throw<InvalidDataException>().WithMessage("*weapon*");
    }

    [Fact]
    public void RefusesAnUnknownFragmentNamingTheFile()
    {
        DataAssets data = _builder.WithFile("Items/Key.fvdata", """{ "type": "item", "fragments": [ { "$type": "edible" } ] }""").Build();

        Action load = () => data.Load<TestItem>("Items/Key.fvdata");

        load.Should().Throw<InvalidDataException>().WithMessage("*Items/Key.fvdata*");
    }

    [Fact]
    public void LoadsTheRowsOfATable()
    {
        DataAssets data = _builder
            .WithFile("Prices.fvtable", """{ "type": "price", "rows": { "Bread": { "price": 2 }, "Lamp": { "price": 15 } } }""")
            .Build();

        DataTable<PriceRow> prices = data.LoadTable<PriceRow>("Prices.fvtable");

        prices.Rows.Keys.Should().Equal("Bread", "Lamp");
        prices.Rows["Lamp"].Price.Should().Be(15);
    }

    [Fact]
    public void RefusesATableOfAnotherRowType()
    {
        DataAssets data = _builder.WithFile("Prices.fvtable", """{ "type": "faction", "rows": {} }""").Build();

        Action load = () => data.LoadTable<PriceRow>("Prices.fvtable");

        load.Should().Throw<InvalidDataException>().WithMessage("*Prices.fvtable*");
    }

    [Fact]
    public void ReadsTheFileAgainAfterItChanges()
    {
        DataAssets data = _builder.WithFile("Items/Key.fvdata", Key).Build();
        TestItem before = data.Load<TestItem>("Items/Key.fvdata");

        _builder.ChangeFile("Items/Key.fvdata", """{ "type": "item", "id": "Item.Key.Attic" }""");

        TestItem after = data.Load<TestItem>("Items/Key.fvdata");
        after.Should().NotBeSameAs(before);
        after.Id.Should().Be(new Tag("Item.Key.Attic"));
    }

    [Fact]
    public void KeepsOneObjectPerFileUntilItChanges()
    {
        DataAssets data = _builder.WithFile("Items/Key.fvdata", Key).Build();

        TestItem first = data.Load<TestItem>("Items/Key.fvdata");
        TestItem second = data.Load<TestItem>("Items/Key.fvdata");

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void WritesAnAssetAsAFileThatReadsBack()
    {
        DataAssets data = _builder.WithFile("Items/Key.fvdata", Key).Build();
        TestItem key = data.Load<TestItem>("Items/Key.fvdata");

        byte[] written = _builder.Types.WriteAsset(key, "../.schemas/data.schema.json");

        JsonObject file = JsonNode.Parse(written)!.AsObject();
        file["$schema"]!.GetValue<string>().Should().Be("../.schemas/data.schema.json");
        file["type"]!.GetValue<string>().Should().Be("item");
        file["fragments"]![1]!["$type"]!.GetValue<string>().Should().Be("usable");
        TestItem read = (TestItem)_builder.Types.ReadAsset("Items/Key.fvdata", file);
        read.Id.Should().Be(key.Id);
        read.Fragments[1].Should().BeOfType<UsableFragment>().Which.Uses.Should().Be(3);
    }

    [Fact]
    public void FindsADefinitionByItsId()
    {
        DataAssets data = _builder
            .WithFile("Items/Key.fvdata", Key)
            .WithFile("Items/Lamp.fvdata", """{ "type": "item", "id": "Item.Lamp" }""")
            .WithFile("Factions/Guard.fvdata", """{ "type": "faction", "id": "Item.Lamp" }""")
            .Build();

        TestItem? lamp = data.Find<TestItem>(new Tag("Item.Lamp"));
        TestItem? missing = data.Find<TestItem>(new Tag("Item.Sword"));

        lamp.Should().NotBeNull();
        lamp!.Id.Should().Be(new Tag("Item.Lamp"));
        missing.Should().BeNull();
    }

    [Fact]
    public void LoadsEveryAssetOfATypeSkippingBrokenFiles()
    {
        DataAssets data = _builder
            .WithFile("Items/Key.fvdata", Key)
            .WithFile("Items/Lamp.fvdata", """{ "type": "item", "id": "Item.Lamp" }""")
            .WithFile("Items/Broken.fvdata", """{ "type": "item", "slots": 3 }""")
            .WithFile(".play/Old.fvdata", """{ "type": "item", "id": "Item.Old" }""")
            .Build();

        IReadOnlyList<TestItem> items = data.LoadAll<TestItem>();

        items.Select(static item => item.Id.Name).Should().BeEquivalentTo(["Item.Key.Cellar", "Item.Lamp"]);
    }

    [Fact]
    public void FindsAFileAddedAfterTheFirstLookup()
    {
        DataAssets data = _builder.WithFile("Items/Key.fvdata", Key).Build();
        data.LoadAll<TestItem>();

        _builder.ChangeFile("Items/Lamp.fvdata", """{ "type": "item", "id": "Item.Lamp" }""");

        data.Find<TestItem>(new Tag("Item.Lamp")).Should().NotBeNull();
    }

    [Fact]
    public void DescribesEveryAssetTypeWithItsFragmentsInTheSchema()
    {
        JsonObject schema = _builder.Types.GetAssetSchema();

        string text = schema.ToJsonString();
        text.Should().Contain("\"const\":\"item\"").And.Contain("\"const\":\"faction\"");
        text.Should().Contain("\"pickup\"").And.Contain("\"usable\"");
    }

    [Fact]
    public void DescribesTablesWithTheirRowsInTheSchema()
    {
        JsonObject schema = _builder.Types.GetTableSchema();

        string text = schema.ToJsonString();
        text.Should().Contain("\"const\":\"price\"").And.Contain("\"price\":{");
    }

    [Fact]
    public void RefusesRegistrationsOnceFilesWereRead()
    {
        DataAssets data = _builder.WithFile("Items/Key.fvdata", Key).Build();
        data.Load<TestItem>("Items/Key.fvdata");

        Action register = () => _builder.Types.Register<PriceRow>("late");

        register.Should().Throw<InvalidOperationException>();
    }
}
