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
        ContentManager content = _builder.WithFile("Scenes/Room.fvscene", """{ "entities": [ { "name": "Crate" } ] }""").Build();

        SceneData scene = content.Load("Scenes/Room.fvscene", RuntimeJsonContext.Default.SceneData);

        scene.Entities.Should().ContainSingle().Which.Name.Should().Be("Crate");
    }

    [Fact]
    public void ReturnsTheSameObjectUntilUnloaded()
    {
        ContentManager content = _builder.WithFile("Room.fvscene", """{ "entities": [] }""").Build();

        SceneData first = content.Load("Room.fvscene", RuntimeJsonContext.Default.SceneData);
        SceneData again = content.Load("./Room.fvscene", RuntimeJsonContext.Default.SceneData);
        bool unloaded = content.Unload("Room.fvscene");
        SceneData reloaded = content.Load("Room.fvscene", RuntimeJsonContext.Default.SceneData);

        again.Should().BeSameAs(first);
        unloaded.Should().BeTrue();
        reloaded.Should().NotBeSameAs(first);
    }

    [Theory]
    [InlineData("../Room.fvscene")]
    [InlineData("Scenes/../../Room.fvscene")]
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

        string fullPath = content.GetFullPath("..Room.fvscene");

        string expected = Path.Combine(_builder.RootDirectory, "..Room.fvscene");
        fullPath.Should().Be(expected);
    }

    [Fact]
    public void JsonErrorsNameTheFile()
    {
        ContentManager content = _builder.WithFile("Broken.fvscene", """{ "entities": [ { "nam": "Crate" } ] }""").Build();

        Action load = () => content.Load("Broken.fvscene", RuntimeJsonContext.Default.SceneData);

        load.Should().Throw<InvalidDataException>().WithMessage("Broken.fvscene: *");
    }

    [Fact]
    public void NullContentThrows()
    {
        ContentManager content = _builder.WithFile("Empty.fvscene", "null").Build();

        Action load = () => content.Load("Empty.fvscene", RuntimeJsonContext.Default.SceneData);

        load.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ForgetsAChangedFileAndTellsWhoListens()
    {
        ContentManager content = _builder.WithFile("Room.fvscene", """{ "entities": [] }""").Build();
        SceneData before = content.Load("Room.fvscene", RuntimeJsonContext.Default.SceneData);
        List<string> changed = [];
        content.Changed += changed.Add;

        _builder.WithFile("Room.fvscene", """{ "entities": [ { "name": "Lamp" } ] }""");
        content.NotifyChanged("Room.fvscene");
        SceneData after = content.Load("Room.fvscene", RuntimeJsonContext.Default.SceneData);

        string fullPath = content.GetFullPath("Room.fvscene");
        changed.Should().Equal(fullPath);
        after.Should().NotBeSameAs(before);
        after.Entities.Should().ContainSingle().Which.Name.Should().Be("Lamp");
    }

    [Fact]
    public void WritesAFileAndTellsWhoListens()
    {
        ContentManager content = _builder.Build();
        List<string> changed = [];
        content.Changed += changed.Add;

        content.Write("Scenes/Room.fvscene", "{}"u8.ToArray());

        string fullPath = Path.Combine(_builder.RootDirectory, "Scenes", "Room.fvscene");
        File.ReadAllText(fullPath).Should().Be("{}");
        changed.Should().Equal(fullPath);
    }

    [Fact]
    public void WritesTheSourceFileTooInADevelopmentBuild()
    {
        string source = _builder.WithSourceFolder("Game");
        ContentManager content = _builder.Build();

        content.Write("Game/Scenes/Room.fvscene", "{}"u8.ToArray());

        string sourcePath = Path.Combine(source, "Scenes", "Room.fvscene");
        File.ReadAllText(sourcePath).Should().Be("{}");
        string builtPath = Path.Combine(_builder.RootDirectory, "Game", "Scenes", "Room.fvscene");
        File.ReadAllText(builtPath).Should().Be("{}");
    }

    [Fact]
    public void WritesOnlyTheBuiltFileOutsideTheSourceFolders()
    {
        string source = _builder.WithSourceFolder("Game");
        ContentManager content = _builder.Build();

        content.Write("Other/Room.fvscene", "{}"u8.ToArray());

        Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }
}
