using FlickerVoid.Engine.Gameplay.Localization;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Debugging;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Localization;

/// <summary>
/// Turns texts into what the player sees in the culture <see cref="Culture"/> (<c>loc.Culture</c>) names: its translation from the
/// string tables of the content's <see cref="Folder"/>, or else the text as written. The tables are read again when they change. Texts
/// written in code, such as the interface's labels, are declared with <see cref="Declare"/>, so the content check and translators know them.
/// </summary>
public sealed class TextLocalizer
{
    /// <summary>The content folder holding the string tables.</summary>
    public const string Folder = "Localization";

    private readonly List<LocalizedText> _declared = [];
    private readonly Dictionary<string, Dictionary<string, StringTableEntry>> _translations = new(StringComparer.OrdinalIgnoreCase);
    private IContentManager? _content;

    public TextLocalizer()
    {
        Culture.Changed += _ => Changed?.Invoke();
    }

    /// <summary>Raised when the texts shown may have changed: another culture was chosen or a table changed.</summary>
    public event Action? Changed;

    /// <summary>The culture texts are shown in; empty, or one without translations, shows them as written.</summary>
    public ConsoleVariable<string> Culture { get; } = ConsoleVariable.Create("loc.Culture", "The language texts are shown in, such as pl; empty shows them as written.", "");

    /// <summary>The cultures the tables translate into, in order.</summary>
    public IReadOnlyList<string> Cultures
    {
        get
        {
            List<string> cultures = [.. _translations.Keys];
            cultures.Sort(StringComparer.OrdinalIgnoreCase);
            return cultures;
        }
    }

    /// <summary>The texts written in code, as declared.</summary>
    public IReadOnlyList<LocalizedText> Declared => _declared;

    /// <summary>Declares texts written in code, which content doesn't hold.</summary>
    public void Declare(params IEnumerable<LocalizedText> texts)
    {
        _declared.AddRange(texts);
    }

    /// <summary>Reads the content's string tables, and again whenever one of them changes.</summary>
    public void Load(IContentManager content)
    {
        if (_content is not null)
        {
            _content.Changed -= HandleContentChanged;
        }

        _content = content;
        content.Changed += HandleContentChanged;
        Read();
    }

    /// <summary>The text in the chosen culture, or as written when it has no translation.</summary>
    public string Get(LocalizedText text)
    {
        if (text.Key.Length > 0 && FindTranslation(text.Key) is { } translated)
        {
            return translated;
        }

        return text.Text;
    }

    private string? FindTranslation(string key)
    {
        if (Culture.Value.Length == 0 || !_translations.TryGetValue(Culture.Value, out Dictionary<string, StringTableEntry>? texts))
        {
            return null;
        }

        if (texts.TryGetValue(key, out StringTableEntry? entry) && entry.Text.Length > 0)
        {
            return entry.Text;
        }

        return null;
    }

    private void Read()
    {
        _translations.Clear();
        string folder = _content!.GetFullPath(Folder);
        if (!Directory.Exists(folder))
        {
            return;
        }

        List<string> files = [.. Directory.EnumerateFiles(folder, "*" + StringTable.Extension, SearchOption.AllDirectories)];
        files.Sort(StringComparer.Ordinal);
        foreach (string file in files)
        {
            string relative = Path.GetRelativePath(_content.RootDirectory, file);
            _content.Unload(relative);
            StringTable table = _content.Load(relative, RuntimeJsonContext.Default.StringTable);
            Add(table);
        }
    }

    private void Add(StringTable table)
    {
        if (table.Culture.Length == 0)
        {
            return;
        }

        if (!_translations.TryGetValue(table.Culture, out Dictionary<string, StringTableEntry>? texts))
        {
            texts = new Dictionary<string, StringTableEntry>(StringComparer.Ordinal);
            _translations.Add(table.Culture, texts);
        }

        foreach ((string key, StringTableEntry entry) in table.Texts)
        {
            texts[key] = entry;
        }
    }

    private void HandleContentChanged(string fullPath)
    {
        if (!fullPath.EndsWith(StringTable.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Read();
        Changed?.Invoke();
    }
}
