using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlickerVoid.Editor.Documents;

/// <summary>
/// The document tabs over the viewport (rule R6, everything opens in place): the level first, then one tab per opened editor;
/// selecting one shows its editor where the viewport was.
/// </summary>
public sealed partial class DocumentTabs : ObservableObject
{
    public ObservableCollection<EditorDocument> Documents { get; } = [];

    /// <summary>The document shown in place of the viewport, or null while the level is.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLevelShown), nameof(HasSide))]
    public partial EditorDocument? Selected { get; set; }

    public bool IsLevelShown => Selected is null;

    /// <summary>Whether the shown document brings its own side column.</summary>
    public bool HasSide => Selected?.Side is not null;

    /// <summary>Whether the tab strip shows: only once a document is open, so the level alone takes the whole height.</summary>
    public bool HasDocuments => Documents.Count > 0;

    /// <summary>Selects the tab of the document's file, opening the document when its file has none.</summary>
    public EditorDocument Show(EditorDocument document)
    {
        EditorDocument? open = Find(document.Key);
        if (open is null)
        {
            Documents.Add(document);
            OnPropertyChanged(nameof(HasDocuments));
            open = document;
        }

        Selected = open;
        return open;
    }

    /// <summary>The open document with the key, if any.</summary>
    public EditorDocument? Find(string key) =>
        Documents.FirstOrDefault(document => string.Equals(document.Key, key, StringComparison.OrdinalIgnoreCase));

    [RelayCommand]
    public void Close(EditorDocument? document)
    {
        if (document is null || !Documents.Remove(document))
        {
            return;
        }

        if (Selected == document)
        {
            Selected = null;
        }

        OnPropertyChanged(nameof(HasDocuments));
        document.Closed();
    }

    [RelayCommand]
    public void ShowLevel() => Selected = null;
}
