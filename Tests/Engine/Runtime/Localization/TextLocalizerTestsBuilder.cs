using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Localization;

namespace FlickerVoid.Engine.Runtime.Tests.Localization;

/// <summary>Builds the localizer under test over a temporary content folder of the string tables given, which it deletes when disposed.</summary>
internal sealed class TextLocalizerTestsBuilder : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FlickerVoidTextLocalizerTests", Guid.NewGuid().ToString("N"));

    public TextLocalizerTestsBuilder()
    {
        Directory.CreateDirectory(_folder);
        Content = new ContentManager(_folder);
    }

    public ContentManager Content { get; }

    /// <summary>A string table in the localization folder.</summary>
    public TextLocalizerTestsBuilder WithTable(string name, string json)
    {
        string path = $"{TextLocalizer.Folder}/{name}";
        WriteFile(path, json);
        return this;
    }

    public void WriteFile(string path, string json)
    {
        string fullPath = Path.Combine(_folder, path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(fullPath, json);
    }

    /// <summary>A localizer that has read the folder's tables.</summary>
    public TextLocalizer Build()
    {
        TextLocalizer localizer = new();
        localizer.Load(Content);
        return localizer;
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
