using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

public sealed class ContentManagerTests : IDisposable
{
    private readonly string _root;
    private readonly ContentManager _content;

    public ContentManagerTests()
    {
        string folderName = Guid.NewGuid().ToString("N");
        string tempPath = Path.GetTempPath();
        _root = Path.Combine(tempPath, folderName);
        Directory.CreateDirectory(_root);
        _content = new ContentManager(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void LoadsJsonByPathUnderTheRoot()
    {
        Write("Scenes/Room.scene.json", """{ "entities": [ { "name": "Crate" } ] }""");

        SceneData scene = _content.Load("Scenes/Room.scene.json", RuntimeJsonContext.Default.SceneData);

        Assert.Equal("Crate", scene.Entities[0].Name);
    }

    [Fact]
    public void ReturnsTheSameObjectUntilUnloaded()
    {
        Write("Room.scene.json", """{ "entities": [] }""");

        SceneData first = _content.Load("Room.scene.json", RuntimeJsonContext.Default.SceneData);
        SceneData again = _content.Load("./Room.scene.json", RuntimeJsonContext.Default.SceneData);
        bool unloaded = _content.Unload("Room.scene.json");
        SceneData reloaded = _content.Load("Room.scene.json", RuntimeJsonContext.Default.SceneData);

        Assert.Same(first, again);
        Assert.True(unloaded);
        Assert.NotSame(first, reloaded);
    }

    [Fact]
    public void RejectsPathsOutsideTheRoot()
    {
        string outside = Path.GetTempPath();

        Assert.Throws<ArgumentException>(() => _content.GetFullPath("../Room.scene.json"));
        Assert.Throws<ArgumentException>(() => _content.GetFullPath("Scenes/../../Room.scene.json"));
        Assert.Throws<ArgumentException>(() => _content.GetFullPath(outside));
    }

    [Fact]
    public void AcceptsNamesThatOnlyStartWithDots()
    {
        string fullPath = _content.GetFullPath("..Room.scene.json");

        string expected = Path.Combine(_root, "..Room.scene.json");
        Assert.Equal(expected, fullPath);
    }

    [Fact]
    public void JsonErrorsNameTheFile()
    {
        Write("Broken.scene.json", """{ "entities": [ { "nam": "Crate" } ] }""");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => _content.Load("Broken.scene.json", RuntimeJsonContext.Default.SceneData));

        Assert.StartsWith("Broken.scene.json: ", exception.Message);
    }

    [Fact]
    public void NullContentThrows()
    {
        Write("Empty.scene.json", "null");

        Assert.Throws<InvalidDataException>(() => _content.Load("Empty.scene.json", RuntimeJsonContext.Default.SceneData));
    }

    private void Write(string path, string json)
    {
        string fullPath = Path.Combine(_root, path);
        string? folder = Path.GetDirectoryName(fullPath);
        if (folder is not null)
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(fullPath, json);
    }
}
