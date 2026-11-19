using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

public sealed class ContentManagerTests : IDisposable
{
    private readonly ContentManagerTestsBuilder _builder = new();

    public void Dispose()
    {
        _builder.Dispose();
    }

    [Fact]
    public void LoadsJsonByPathUnderTheRoot()
    {
        ContentManager content = _builder.WithFile("Scenes/Room.scene.json", """{ "entities": [ { "name": "Crate" } ] }""").Build();

        SceneData scene = content.Load("Scenes/Room.scene.json", RuntimeJsonContext.Default.SceneData);

        scene.Entities.Should().ContainSingle().Which.Name.Should().Be("Crate");
    }

    [Fact]
    public void ReturnsTheSameObjectUntilUnloaded()
    {
        ContentManager content = _builder.WithFile("Room.scene.json", """{ "entities": [] }""").Build();

        SceneData first = content.Load("Room.scene.json", RuntimeJsonContext.Default.SceneData);
        SceneData again = content.Load("./Room.scene.json", RuntimeJsonContext.Default.SceneData);
        bool unloaded = content.Unload("Room.scene.json");
        SceneData reloaded = content.Load("Room.scene.json", RuntimeJsonContext.Default.SceneData);

        again.Should().BeSameAs(first);
        unloaded.Should().BeTrue();
        reloaded.Should().NotBeSameAs(first);
    }

    [Theory]
    [InlineData("../Room.scene.json")]
    [InlineData("Scenes/../../Room.scene.json")]
    public void RejectsPathsOutsideTheRoot(string path)
    {
        ContentManager content = _builder.Build();

        Action resolve = () => content.GetFullPath(path);

        resolve.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RejectsRootedPathsOutsideTheRoot()
    {
        ContentManager content = _builder.Build();
        string outside = Path.GetTempPath();

        Action resolve = () => content.GetFullPath(outside);

        resolve.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AcceptsNamesThatOnlyStartWithDots()
    {
        ContentManager content = _builder.Build();

        string fullPath = content.GetFullPath("..Room.scene.json");

        string expected = Path.Combine(_builder.RootDirectory, "..Room.scene.json");
        fullPath.Should().Be(expected);
    }

    [Fact]
    public void JsonErrorsNameTheFile()
    {
        ContentManager content = _builder.WithFile("Broken.scene.json", """{ "entities": [ { "nam": "Crate" } ] }""").Build();

        Action load = () => content.Load("Broken.scene.json", RuntimeJsonContext.Default.SceneData);

        load.Should().Throw<InvalidDataException>().WithMessage("Broken.scene.json: *");
    }

    [Fact]
    public void NullContentThrows()
    {
        ContentManager content = _builder.WithFile("Empty.scene.json", "null").Build();

        Action load = () => content.Load("Empty.scene.json", RuntimeJsonContext.Default.SceneData);

        load.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ForgetsAChangedFileAndTellsWhoListens()
    {
        ContentManager content = _builder.WithFile("Room.scene.json", """{ "entities": [] }""").Build();
        SceneData before = content.Load("Room.scene.json", RuntimeJsonContext.Default.SceneData);
        List<string> changed = [];
        content.Changed += changed.Add;

        _builder.WithFile("Room.scene.json", """{ "entities": [ { "name": "Lamp" } ] }""");
        content.NotifyChanged("Room.scene.json");
        SceneData after = content.Load("Room.scene.json", RuntimeJsonContext.Default.SceneData);

        string fullPath = content.GetFullPath("Room.scene.json");
        changed.Should().Equal(fullPath);
        after.Should().NotBeSameAs(before);
        after.Entities.Should().ContainSingle().Which.Name.Should().Be("Lamp");
    }
}
