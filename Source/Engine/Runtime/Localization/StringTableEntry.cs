namespace FlickerVoid.Engine.Runtime.Localization;

/// <summary>One translation of a string table.</summary>
public sealed class StringTableEntry
{
    public string Text { get; set; } = "";

    /// <summary>The text as written that this translates, so a text rewritten since shows as out of date.</summary>
    public string Source { get; set; } = "";

    /// <summary>The translator's own remark.</summary>
    public string Note { get; set; } = "";
}
