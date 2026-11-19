using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

public sealed class ContentBuildManifestTests : IDisposable
{
    private readonly ContentBuildManifestTestsBuilder _builder = new();

    public void Dispose()
    {
        _builder.Dispose();
    }

    [Fact]
    public void ReadsTheBuilderAndTheFoldersItKeepsBuilt()
    {
        _builder.WithLines(
            "dotnet=/usr/bin/dotnet",
            "builder=/engine/Builder.dll",
            "folder=/game/Content|",
            "folder=/engine/Rendering/Shaders|Engine/Rendering");

        ContentBuildManifest? manifest = _builder.Find();

        manifest.Should().NotBeNull();
        manifest!.Dotnet.Should().Be("/usr/bin/dotnet");
        manifest.Builder.Should().Be("/engine/Builder.dll");
        string shaders = _builder.FullPath("Engine/Rendering");
        manifest.Folders.Should().Equal(
            new ContentBuildFolder("/game/Content", _builder.RootDirectory),
            new ContentBuildFolder("/engine/Rendering/Shaders", shaders));
    }

    [Fact]
    public void FindsNoneInContentThatWasPublished()
    {
        ContentBuildManifest? manifest = _builder.Find();

        manifest.Should().BeNull();
    }
}
