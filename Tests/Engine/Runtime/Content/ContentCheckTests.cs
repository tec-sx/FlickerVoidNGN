using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

public sealed class ContentCheckTests : IDisposable
{
    private readonly ContentCheckTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void FindsNothingWrongWithContentThatReadsAndResolves()
    {
        _builder
            .WithFile("Scenes/Town.fvscene", """
                // A town square, with comments and a trailing comma as content files may have.
                { "$schema": "../.schemas/scene.schema.json", "entities": [
                    { "name": "Well", "components": { "transform": {}, "saveable": { "id": "well" } } },
                    { "name": "Sign", "parent": "Well", "template": "Templates/Sign.fventity", "components": { "signpost": { "target": "Well" } } }, ] }
                """)
            .WithFile("Templates/Sign.fventity", """{ "components": { "signpost": { "note": "Journeys/Home.fvdata" } } }""")
            .WithFile("Journeys/Home.fvdata", """{ "type": "journey", "legs": [ { "$type": "walk", "scene": "Scenes/Town.fvscene" } ] }""")
            .WithFile("Fares.fvtable", """{ "type": "fare", "rows": { "Short": { "price": 2, "journey": "Journeys/Home.fvdata" } } }""")
            .WithFile("Home.fvtrail", """{ "journey": "Journeys/Home.fvdata" }""")
            .WithFile(".schemas/scene.schema.json", "{ not read");

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().BeEmpty();
    }

    [Fact]
    public void ReportsFilesThatCantBeReadAsTheGameReadsThem()
    {
        _builder
            .WithFile("Scenes/Lamps.fvscene", """
                { "entities": [
                    { "name": "Lamp", "components": { "lamp": {} } },
                    { "name": "Post", "components": { "transform": { "position": "up" } } } ] }
                """)
            .WithFile("Broken.fvscene", """{ "entities": [""")
            .WithFile("Journeys/Flight.fvdata", """{ "type": "flight" }""")
            .WithFile("Fares.fvtable", """{ "type": "fare", "rows": { "Long": { "price": "lots" } } }""")
            .WithFile("Home.fvtrail", """{ "length": 3 }""");

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().Contain(problem => problem.Path == "Scenes/Lamps.fvscene" && problem.Message.Contains("Lamp") && problem.Message.Contains("lamp"));
        problems.Should().Contain(problem => problem.Path == "Scenes/Lamps.fvscene" && problem.Message.Contains("Post") && problem.Message.Contains("transform"));
        problems.Select(problem => problem.Path).Should().Contain(["Broken.fvscene", "Journeys/Flight.fvdata", "Fares.fvtable", "Home.fvtrail"]);
        problems.Should().HaveCount(6);
    }

    [Fact]
    public void ReportsContentFilesNamedThatTheFolderLacksOrOfAnotherKind()
    {
        _builder
            .WithFile("Scenes/Town.fvscene", """
                { "entities": [
                    { "name": "Sign", "components": { "signpost": { "note": "Journeys/Gone.fvdata" } } },
                    { "name": "Board", "components": { "signpost": { "note": "Scenes/Town.fvscene" } } },
                    { "name": "Stall", "template": "Templates/Stall.fventity", "components": {} } ] }
                """)
            .WithFile("Journeys/Home.fvdata", """{ "type": "journey", "legs": [ { "$type": "walk", "scene": "Scenes/Market.fvscene" } ] }""")
            .WithFile("Fares.fvtable", """{ "type": "fare", "rows": { "Short": { "price": 2, "journey": "Journeys/Away.fvdata" } } }""")
            .WithFile("Home.fvtrail", """{ "journey": "Journeys/Lost.fvdata" }""")
            .WithFile("Templates/Sign.fventity", """{ "base": "Templates/Post.fventity", "components": {} }""");

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().Contain(problem => problem.Path == "Scenes/Town.fvscene" && problem.Message.Contains("Sign") && problem.Message.Contains("Journeys/Gone.fvdata"));
        problems.Should().Contain(problem => problem.Path == "Scenes/Town.fvscene" && problem.Message.Contains("Board") && problem.Message.Contains(".fvdata"));
        problems.Should().Contain(problem => problem.Path == "Scenes/Town.fvscene" && problem.Message.Contains("Templates/Stall.fventity"));
        problems.Should().Contain(problem => problem.Path == "Journeys/Home.fvdata" && problem.Message.Contains("legs[0].scene") && problem.Message.Contains("Scenes/Market.fvscene"));
        problems.Should().Contain(problem => problem.Path == "Fares.fvtable" && problem.Message.Contains("Short") && problem.Message.Contains("Journeys/Away.fvdata"));
        problems.Should().Contain(problem => problem.Path == "Home.fvtrail" && problem.Message.Contains("Journeys/Lost.fvdata"));
        problems.Should().Contain(problem => problem.Path == "Templates/Sign.fventity" && problem.Message.Contains("Templates/Post.fventity"));
        problems.Should().HaveCount(7);
    }

    [Fact]
    public void ReportsEntitiesNamedThatTheirSceneLacksAndNamesOrSaveIdsTwoShare()
    {
        _builder.WithFile("Scenes/Town.fvscene", """
            { "entities": [
                { "name": "Sign", "parent": "Gate", "components": { "signpost": { "target": "Well" } } },
                { "name": "Lamp", "components": {} },
                { "name": "Lamp", "components": {} },
                { "name": "Bench", "components": { "saveable": { "id": "seat" } } },
                { "name": "Stool", "components": { "saveable": { "id": "seat" } } } ] }
            """);

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().Contain(problem => problem.Message.Contains("Sign") && problem.Message.Contains("Gate"));
        problems.Should().Contain(problem => problem.Message.Contains("Sign") && problem.Message.Contains("Well"));
        problems.Should().Contain(problem => problem.Message.Contains("Lamp"));
        problems.Should().Contain(problem => problem.Message.Contains("seat"));
        problems.Should().HaveCount(4).And.OnlyContain(problem => problem.Path == "Scenes/Town.fvscene");
    }

    [Fact]
    public void FindsNothingWrongWithKeyedTextsAndTheirTranslations()
    {
        _builder
            .WithFile("Journeys/Home.fvdata", """{ "type": "journey", "title": { "key": "Journey.Home", "text": "Home", "note": "A journey's name." } }""")
            .WithFile("Journeys/Back.fvdata", """{ "type": "journey", "title": { "key": "Journey.Home", "text": "Home" } }""")
            .WithFile("Journeys/Untitled.fvdata", """{ "type": "journey" }""")
            .WithDeclaredText("Hub.Close", "Close")
            .WithFile("Localization/Polish.fvstrings", """
                { "culture": "pl", "texts": { "Journey.Home": { "text": "Dom", "source": "Home" }, "Hub.Close": { "text": "Zamknij", "source": "Close" } } }
                """);

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().BeEmpty();
    }

    [Fact]
    public void ReportsTextsWithoutKeysAndKeysGivenToTwoTextsInContentOrCode()
    {
        _builder
            .WithFile("Journeys/Home.fvdata", """{ "type": "journey", "title": { "text": "Home" } }""")
            .WithFile("Journeys/Away.fvdata", """{ "type": "journey", "title": { "key": "Journey.Name", "text": "Away" } }""")
            .WithFile("Journeys/Back.fvdata", """{ "type": "journey", "title": { "key": "Journey.Name", "text": "Back" } }""")
            .WithDeclaredText("Journey.Away", "Away")
            .WithFile("Journeys/Off.fvdata", """{ "type": "journey", "title": { "key": "Journey.Away", "text": "Off" } }""");

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().Contain(problem => problem.Path == "Journeys/Home.fvdata" && problem.Message.Contains("title") && problem.Message.Contains("key"));
        problems.Should().Contain(problem => problem.Path == "Journeys/Back.fvdata" && problem.Message.Contains("Journey.Name") && problem.Message.Contains("Journeys/Away.fvdata"));
        problems.Should().Contain(problem => problem.Path == "Journeys/Off.fvdata" && problem.Message.Contains("Journey.Away") && problem.Message.Contains(ContentCheck.CodePath));
        problems.Should().HaveCount(3);
    }

    [Fact]
    public void ReportsTranslationsOfKeysNoTextHas()
    {
        _builder
            .WithFile("Journeys/Home.fvdata", """{ "type": "journey", "title": { "key": "Journey.Home", "text": "Home" } }""")
            .WithFile("Localization/Polish.fvstrings", """
                { "culture": "pl", "texts": { "Journey.Home": { "text": "Dom" }, "Journey.Gone": { "text": "Precz" } } }
                """)
            .WithFile("Localization/Broken.fvstrings", """{ "culture": "de", "texts": [] }""")
            .WithFile("Localization/Nowhere.fvstrings", """{ "texts": {} }""");

        IReadOnlyList<ContentProblem> problems = _builder.Check();

        problems.Should().Contain(problem => problem.Path == "Localization/Polish.fvstrings" && problem.Message.Contains("Journey.Gone"));
        problems.Should().Contain(problem => problem.Path == "Localization/Broken.fvstrings");
        problems.Should().Contain(problem => problem.Path == "Localization/Nowhere.fvstrings" && problem.Message.Contains("culture"));
        problems.Should().HaveCount(3);
    }
}
