namespace FlickerVoid.Engine.Builder.Tests;

public sealed class AssetBuilderTests : IDisposable
{
    private readonly AssetBuilderTestsBuilder _files = new();

    public void Dispose()
    {
        _files.Dispose();
    }

    [Fact]
    public void ImportsFilesWithTheImporterForTheirExtension()
    {
        _files.WithSource("Notes/Hello.txt", "hello");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Built.Should().Equal("Notes/Hello.txt");
        _files.ReadOutput("Notes/Hello.upper").Should().Be("HELLO");
        _files.OutputExists("Notes/Hello.txt").Should().BeFalse();
    }

    [Fact]
    public void LeavesTheSchemaFolderOut()
    {
        _files.WithSource(".schemas/scene.schema.json", "{}").WithSource("A.txt", "a");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Built.Should().Equal("A.txt");
    }

    [Fact]
    public void CopiesFilesNoImporterTakes()
    {
        _files.WithSource("Scenes/Room.scene.json", """{ "entities": [] }""");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Built.Should().Equal("Scenes/Room.scene.json");
        _files.ReadOutput("Scenes/Room.scene.json").Should().Be("""{ "entities": [] }""");
    }

    [Fact]
    public void SkipsFilesThatHaveNotChanged()
    {
        _files.WithSource("A.txt", "a").WithSource("B.txt", "b");
        AssetBuilder builder = _files.Build();
        _files.BuildAll(builder);
        _files.WriteSource("B.txt", "b2");

        BuildReport report = _files.BuildAll(builder);

        report.Built.Should().Equal("B.txt");
        report.Skipped.Should().Equal("A.txt");
        _files.Importer.Imported.Should().Equal("A.txt", "B.txt", "B.txt");
        _files.ReadOutput("B.upper").Should().Be("B2");
    }

    [Fact]
    public void SkipsUnchangedFilesInANewBuilderFromTheCacheItLeftBehind()
    {
        _files.WithSource("A.txt", "a");
        _files.BuildAll(_files.Build());

        BuildReport report = _files.BuildAll(_files.Build());

        report.Skipped.Should().Equal("A.txt");
    }

    [Fact]
    public void RebuildsAFileWhenAFileItReadsChanges()
    {
        _files.WithSource("Room.txt", "room\ninclude Parts/Door.part").WithSource("Parts/Door.part", "door");
        AssetBuilder builder = _files.Build();
        _files.BuildAll(builder);
        _files.WriteSource("Parts/Door.part", "red door");

        BuildReport report = _files.BuildAll(builder);

        report.Built.Should().Contain("Room.txt");
        _files.ReadOutput("Room.upper").Should().Be("ROOM\nRED DOOR");
    }

    [Fact]
    public void RebuildsEverythingAnImporterMadeWhenItsVersionChanges()
    {
        _files.WithSource("A.txt", "a");
        AssetBuilder builder = _files.Build();
        _files.BuildAll(builder);
        _files.Importer.Version = 2;

        BuildReport report = _files.BuildAll(builder);

        report.Built.Should().Equal("A.txt");
    }

    [Fact]
    public void RebuildsAFileWhoseOutputWasDeleted()
    {
        _files.WithSource("A.txt", "a");
        AssetBuilder builder = _files.Build();
        _files.BuildAll(builder);
        _files.DeleteOutput("A.upper");

        BuildReport report = _files.BuildAll(builder);

        report.Built.Should().Equal("A.txt");
        _files.OutputExists("A.upper").Should().BeTrue();
    }

    [Fact]
    public void RemovesTheOutputsOfDeletedFiles()
    {
        _files.WithSource("A.txt", "a").WithSource("B.json", "{}");
        AssetBuilder builder = _files.Build();
        _files.BuildAll(builder);
        _files.DeleteSource("A.txt");
        _files.DeleteSource("B.json");

        BuildReport report = _files.BuildAll(builder);

        report.Removed.Should().BeEquivalentTo("A.txt", "B.json");
        _files.OutputExists("A.upper").Should().BeFalse();
        _files.OutputExists("B.json").Should().BeFalse();
    }

    [Fact]
    public void AFailedFileKeepsItsLastOutputAndIsTriedAgainNextBuild()
    {
        _files.WithSource("A.txt", "a").WithSource("B.txt", "b");
        AssetBuilder builder = _files.Build();
        _files.BuildAll(builder);
        _files.WriteSource("A.txt", "fail");
        _files.WriteSource("B.txt", "b2");

        BuildReport failed = _files.BuildAll(builder);
        BuildReport again = _files.BuildAll(builder);

        failed.Failed.Should().ContainSingle().Which.Should().Be(new BuildFailure("A.txt", "The file asks to fail."));
        failed.Built.Should().Equal("B.txt");
        _files.ReadOutput("A.upper").Should().Be("A");
        again.Failed.Should().ContainSingle().Which.Path.Should().Be("A.txt");
    }

    [Fact]
    public void AFileReadFromOutsideTheSourceFolderFailsItsImport()
    {
        _files.WithSource("A.txt", "include ../Outside.part");
        AssetBuilder builder = _files.Build();

        BuildReport report = _files.BuildAll(builder);

        report.Failed.Should().ContainSingle().Which.Message.Should().Contain("outside");
    }

    [Fact]
    public void AnOutputFolderInsideTheSourceFolderThrows()
    {
        AssetBuilder builder = _files.Build();
        string output = Path.Combine(_files.SourceDirectory, "Built");

        Action build = () => builder.Build(_files.SourceDirectory, output);

        build.Should().Throw<ArgumentException>();
    }
}
