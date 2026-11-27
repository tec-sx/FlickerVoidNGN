using FlickerVoid.Engine.Gameplay.Localization;
using FlickerVoid.Engine.Runtime.Localization;

namespace FlickerVoid.Engine.Runtime.Tests.Localization;

public sealed class TextLocalizerTests : IDisposable
{
    private const string Polish = """
        { "culture": "pl", "texts": {
            "Item.Key.Name": { "text": "Klucz", "source": "Key" },
            "Item.Mug.Name": { "text": "", "source": "Mug" } } }
        """;

    private readonly TextLocalizerTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsTextsAsWrittenUntilACultureIsChosen()
    {
        TextLocalizer localizer = _builder.WithTable("Polish.fvstrings", Polish).Build();
        LocalizedText key = new("Item.Key.Name", "Key");

        string shown = localizer.Get(key);

        shown.Should().Be("Key");
    }

    [Fact]
    public void ShowsTheTranslationOfTheChosenCulture()
    {
        TextLocalizer localizer = _builder.WithTable("Polish.fvstrings", Polish).Build();
        LocalizedText key = new("Item.Key.Name", "Key");
        localizer.Culture.TrySet("pl");

        string shown = localizer.Get(key);

        shown.Should().Be("Klucz");
    }

    [Fact]
    public void ShowsTheTextAsWrittenWhereTheCultureHasNoTranslation()
    {
        TextLocalizer localizer = _builder.WithTable("Polish.fvstrings", Polish).Build();
        LocalizedText mug = new("Item.Mug.Name", "Mug");
        LocalizedText cap = new("Item.Cap.Name", "Cap");
        localizer.Culture.TrySet("pl");

        string mugShown = localizer.Get(mug);
        string capShown = localizer.Get(cap);

        mugShown.Should().Be("Mug");
        capShown.Should().Be("Cap");
    }

    [Fact]
    public void ListsTheCulturesItHasTranslationsFor()
    {
        TextLocalizer localizer = _builder.WithTable("Polish.fvstrings", Polish).WithTable("German.fvstrings", """{ "culture": "de", "texts": {} }""").Build();

        localizer.Cultures.Should().Equal("de", "pl");
    }

    [Fact]
    public void TellsWhenTheTextsShownChange()
    {
        TextLocalizer localizer = _builder.WithTable("Polish.fvstrings", Polish).Build();
        int changes = 0;
        localizer.Changed += () => changes++;

        localizer.Culture.TrySet("pl");
        _builder.WriteFile("Localization/Polish.fvstrings", """{ "culture": "pl", "texts": { "Item.Key.Name": { "text": "Kluczyk", "source": "Key" } } }""");
        _builder.Content.NotifyChanged("Localization/Polish.fvstrings");
        string shown = localizer.Get(new LocalizedText("Item.Key.Name", "Key"));

        changes.Should().Be(2);
        shown.Should().Be("Kluczyk");
    }

    [Fact]
    public void ShowsTextsAsWrittenWithoutContent()
    {
        TextLocalizer localizer = new();
        localizer.Culture.TrySet("pl");

        string shown = localizer.Get(new LocalizedText("Item.Key.Name", "Key"));

        shown.Should().Be("Key");
    }
}
