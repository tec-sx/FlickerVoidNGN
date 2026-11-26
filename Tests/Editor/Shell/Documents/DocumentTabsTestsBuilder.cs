using FlickerVoid.Editor.Documents;

namespace FlickerVoid.Editor.Tests.Documents;

/// <summary>Builds the <see cref="DocumentTabs"/> under test and documents to open in it.</summary>
public static class DocumentTabsTestsBuilder
{
    public static DocumentTabs Build() => new();

    public static EditorDocument Document(string key, Action? closed = null)
    {
        string title = Path.GetFileNameWithoutExtension(key);
        return new EditorDocument(key, title, new object(), closed ?? (() => { }));
    }
}
