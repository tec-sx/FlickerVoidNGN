using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Tests;

/// <summary>Builds the mesh sources, with any sources added, over a temporary content folder of baked models and other files.</summary>
internal sealed class MeshSourcesTestsBuilder : IDisposable
{
    private readonly string _contentFolder;
    private readonly List<IMeshSource> _sources = [];

    public MeshSourcesTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
        Content = new ContentManager(_contentFolder);
    }

    public IContentManager Content { get; }

    /// <summary>Writes a model as the content build leaves one named <paramref name="path"/>: baked, with the .fvmodel extension.</summary>
    public MeshSourcesTestsBuilder WithModel(string path, Action<TestModelBuilder> build)
    {
        TestModelBuilder model = new();
        build(model);
        ModelData data = model.Build();
        string builtPath = Path.ChangeExtension(path, ".fvmodel");
        string fullPath = Path.Combine(_contentFolder, builtPath);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        using FileStream stream = File.Create(fullPath);
        ModelFile.Write(data, stream);
        return this;
    }

    public MeshSourcesTestsBuilder WithFile(string path, string text)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(fullPath, text);
        return this;
    }

    public MeshSourcesTestsBuilder WithSource(IMeshSource source)
    {
        _sources.Add(source);
        return this;
    }

    public MeshSources Build()
    {
        MeshSources sources = new();
        foreach (IMeshSource source in _sources)
        {
            sources.Add(source);
        }

        return sources;
    }

    public void Dispose()
    {
        Directory.Delete(_contentFolder, recursive: true);
    }
}
