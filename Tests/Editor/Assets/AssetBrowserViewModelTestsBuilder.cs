using System.Text.Json;

using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Vehicles;
using FlickerVoid.Engine.Vehicles.Serialization;

namespace FlickerVoid.Editor.Assets.Tests;

/// <summary>Builds the asset browser over a temporary content folder.</summary>
internal sealed class AssetBrowserViewModelTestsBuilder : IDisposable
{
    private const string CodeFolderName = "_Code";
    private const string ContentFolderName = "_Content";

    private readonly string _folder;

    public AssetBrowserViewModelTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        _folder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_folder);
    }

    public AssetBrowserViewModelTestsBuilder WithFile(string path, string text = "{}")
    {
        string fullPath = Path.Combine(_folder, path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(fullPath, text);
        return this;
    }

    /// <summary>A file in a source folder that the content folder's build manifest says is built into the output folder.</summary>
    public AssetBrowserViewModelTestsBuilder WithSourceFile(string outputFolder, string path)
    {
        string source = Path.Combine(_folder, "_Source");
        string fullPath = Path.Combine(source, path);
        Directory.CreateDirectory(source);
        File.WriteAllText(fullPath, "{}");
        string manifest = Path.Combine(_folder, ContentBuildManifest.FileName);
        File.WriteAllLines(manifest, [$"folder={source}|{outputFolder}"]);
        return this;
    }

    /// <summary>A file in the project's code folder, which the browser shows as its Code root.</summary>
    public AssetBrowserViewModelTestsBuilder WithCodeFile(string path, string text = "")
    {
        return WithFile(Path.Combine(CodeFolderName, path), text);
    }

    public AssetBrowserViewModel Build() => new(new ContentManager(_folder));

    /// <summary>The browser over the content folder and the code folder.</summary>
    public AssetBrowserViewModel BuildWithCode()
    {
        string code = Path.Combine(_folder, CodeFolderName);
        Directory.CreateDirectory(code);
        return new AssetBrowserViewModel(new ContentManager(Path.Combine(_folder, ContentFolderName)), codeFolder: code);
    }

    /// <summary>A file in the content folder that <see cref="BuildWithCode"/> browses.</summary>
    public AssetBrowserViewModelTestsBuilder WithContentFile(string path)
    {
        return WithFile(Path.Combine(ContentFolderName, path));
    }

    public string ReadCodeFile(string path) => ReadFile(Path.Combine(CodeFolderName, path));

    public bool CodeFileExists(string path) => Exists(Path.Combine(CodeFolderName, path));

    /// <summary>The browser with the test barrel and wine types to create.</summary>
    public AssetBrowserViewModel BuildWithData() => new(new ContentManager(_folder), Data.BarrelJsonContext.CreateTypes());

    public string ReadFile(string path) => File.ReadAllText(Path.Combine(_folder, path));

    /// <summary>Reads a vehicle file as the engine reads one, so a file it would refuse fails the test.</summary>
    public VehicleFile ReadVehicle(string path)
    {
        string text = ReadFile(path);
        return JsonSerializer.Deserialize(text, VehiclesJsonContext.Default.VehicleFile)!;
    }

    public bool Exists(string path) => File.Exists(Path.Combine(_folder, path));

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }
}
