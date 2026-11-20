using System.Text;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

using FlickerVoid.Editor.Assets.Data;

namespace FlickerVoid.Editor.Data;

/// <summary>The form of an opened data asset, or the rows of an opened table with CSV export and import.</summary>
public sealed partial class DataView : UserControl
{
    private static readonly FilePickerFileType CsvFiles = new("CSV") { Patterns = ["*.csv"] };

    public DataView()
    {
        InitializeComponent();
    }

    private async void HandleExportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TableViewModel table || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        string name = Path.GetFileNameWithoutExtension(table.Path);
        FilePickerSaveOptions options = new() { SuggestedFileName = name + ".csv", FileTypeChoices = [CsvFiles] };
        IStorageFile? file = await top.StorageProvider.SaveFilePickerAsync(options);
        if (file is null)
        {
            return;
        }

        string csv = table.ExportCsv();
        await using Stream stream = await file.OpenWriteAsync();
        byte[] bytes = Encoding.UTF8.GetBytes(csv);
        await stream.WriteAsync(bytes);
    }

    private async void HandleImportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TableViewModel table || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        FilePickerOpenOptions options = new() { FileTypeFilter = [CsvFiles] };
        IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0)
        {
            return;
        }

        await using Stream stream = await files[0].OpenReadAsync();
        using StreamReader reader = new(stream);
        string csv = await reader.ReadToEndAsync();
        try
        {
            table.ImportCsv(csv);
        }
        catch (InvalidDataException exception)
        {
            ToolTip.SetTip(this, exception.Message);
        }
    }
}
