using System.Text.Json.Nodes;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

public sealed class ContentTagsTests : IDisposable
{
    private const string Scene = """
        { "entities": [
          { "name": "Door", "components": { "lock": { "key": "Item.Key.Cellar", "opens": ["Place.Cellar", "Place.Inn"], "label": "Item.Key.Cellar" } } },
          { "name": "Gate", "components": { "lock": { "key": "Item.Key" } } }
        ] }
        """;

    private const string Asset = """
        { "type": "errand",
          "title": { "key": "Item.Key.Cellar", "text": "The key" },
          "steps": [ { "$type": "fetch", "item": "Item.Key.Cellar" } ],
          "marks": { "first": "Place.Inn" } }
        """;

    private readonly ContentTagsTestsBuilder _builder = new();

    [Fact]
    public void FindsTheTagsOfASceneComponentsAndNothingThatOnlyLooksLikeOne()
    {
        IReadOnlyList<string> tags = _builder.Tags.Find("Main.fvscene", ContentTagsTestsBuilder.Bytes(Scene));

        tags.Should().Equal("Item.Key.Cellar", "Place.Cellar", "Place.Inn", "Item.Key");
    }

    [Fact]
    public void FindsTheTagsDeepInADataAssetButNotATextsKey()
    {
        IReadOnlyList<string> tags = _builder.Tags.Find("Errand.fvdata", ContentTagsTestsBuilder.Bytes(Asset));

        tags.Should().Equal("Item.Key.Cellar", "Place.Inn");
    }

    [Fact]
    public void RenamesATagAndTheTagsBelowItOnly()
    {
        byte[]? renamed = _builder.Tags.Rename("Main.fvscene", ContentTagsTestsBuilder.Bytes(Scene), "Item.Key", "Item.Keys");

        JsonNode scene = JsonNode.Parse(ContentTagsTestsBuilder.Text(renamed!))!;
        JsonNode door = scene["entities"]![0]!["components"]!["lock"]!;
        ((string)door["key"]!).Should().Be("Item.Keys.Cellar");
        ((string)door["label"]!).Should().Be("Item.Key.Cellar", "a plain name is left alone");
        ((string)scene["entities"]![1]!["components"]!["lock"]!["key"]!).Should().Be("Item.Keys");
        ((string)door["opens"]![0]!).Should().Be("Place.Cellar");
    }

    [Fact]
    public void LeavesAFileWithoutTheTagAsItIs()
    {
        _builder.Tags.Rename("Errand.fvdata", ContentTagsTestsBuilder.Bytes(Asset), "Music", "Tune").Should().BeNull();
    }

    [Fact]
    public void RenamesInADataAssetWithoutTouchingTheTextsKey()
    {
        byte[]? renamed = _builder.Tags.Rename("Errand.fvdata", ContentTagsTestsBuilder.Bytes(Asset), "Item.Key.Cellar", "Item.CellarKey");

        JsonNode asset = JsonNode.Parse(ContentTagsTestsBuilder.Text(renamed!))!;
        ((string)asset["steps"]![0]!["item"]!).Should().Be("Item.CellarKey");
        ((string)asset["title"]!["key"]!).Should().Be("Item.Key.Cellar");
    }

    [Fact]
    public void RemovesATagFromListsAndEmptiesASingleOne()
    {
        byte[]? removed = _builder.Tags.Remove("Main.fvscene", ContentTagsTestsBuilder.Bytes(Scene), "Place.Inn");
        removed = _builder.Tags.Remove("Main.fvscene", removed!, "Item.Key");

        JsonNode scene = JsonNode.Parse(ContentTagsTestsBuilder.Text(removed!))!;
        JsonNode door = scene["entities"]![0]!["components"]!["lock"]!;
        door["opens"]!.AsArray().Select(item => (string)item!).Should().Equal("Place.Cellar");
        ((string)door["key"]!).Should().BeEmpty("removing a tag removes the tags below it");
        ((string)scene["entities"]![1]!["components"]!["lock"]!["key"]!).Should().BeEmpty();
    }

    [Fact]
    public void SkipsFilesItCannotRead()
    {
        _builder.Tags.Find("Broken.fvscene", ContentTagsTestsBuilder.Bytes("{ not json")).Should().BeEmpty();
        _builder.Tags.Find("Notes.txt", ContentTagsTestsBuilder.Bytes("Item.Key")).Should().BeEmpty();
    }

    public void Dispose() => _builder.Dispose();
}
