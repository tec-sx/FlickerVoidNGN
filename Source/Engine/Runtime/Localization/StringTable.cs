namespace FlickerVoid.Engine.Runtime.Localization;

/// <summary>A string table file (<c>.fvstrings</c>) of the localization folder: a translation into one culture of texts by their keys, wherever they are written.</summary>
public sealed class StringTable
{
    public const string Extension = ".fvstrings";

    public const string SchemaName = "strings";

    /// <summary>The culture the texts are translated into (<c>pl</c>, <c>de-AT</c>).</summary>
    public string Culture { get; set; } = "";

    /// <summary>The translations by the keys of the texts they translate.</summary>
    public Dictionary<string, StringTableEntry> Texts { get; set; } = [];
}
