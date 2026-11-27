using System.Text.Json.Serialization;

namespace FlickerVoid.Engine.Gameplay.Localization;

/// <summary>A text the player sees: its key in the string tables, the text as written, and a note of context for its translators.</summary>
public sealed record LocalizedText
{
    public LocalizedText()
    {
    }

    public LocalizedText(string key, string text, string note = "")
    {
        Key = key;
        Text = text;
        Note = note;
    }

    /// <summary>Names the text in translations, dot-separated like a tag (<c>Quest.Cellar.Name</c>); one key is one text.</summary>
    public string Key { get; set; } = "";

    /// <summary>The text as written, shown where the chosen culture has no translation of it.</summary>
    public string Text { get; set; } = "";

    /// <summary>Who says it, where it shows or what it means, for whoever translates it.</summary>
    public string Note { get; set; } = "";

    [JsonIgnore]
    public bool IsEmpty => Text.Length == 0;

    public override string ToString() => Text;
}
