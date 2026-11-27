using FlickerVoid.Editor.Assets.Tags;

namespace FlickerVoid.Editor.Assets.Tests.Tags;

public sealed class ProjectTagsTests : IDisposable
{
    private readonly ProjectTagsTestsBuilder _builder = new();

    [Fact]
    public void FillsTheCatalogWithTheDeclaredTagsAndTheTagsInUseWithTheirCounts()
    {
        _builder.WithFile(ProjectTags.FileName, """{ "tags": ["Ambience.Rain"] }""");

        _builder.Build();

        _builder.Catalog.Names.Should().Equal("Ambience", "Ambience.Rain", "Character", "Character.Fox", "Music", "Music.Drums", "Music.Drums.Cellar");
        _builder.Catalog.UsesOf("Music.Drums").Should().Be(1, "the engine's own content is not the project's");
        _builder.Catalog.UsesBelow("Music").Should().Be(2);
    }

    [Fact]
    public void KeepsATagAddedInThePickerInTheProjectsTagsFile()
    {
        _builder.Build();

        _builder.Catalog.Add("Music.Chase");

        _builder.ReadFile(ProjectTags.FileName).Should().Contain("\"Music.Chase\"");
    }

    [Fact]
    public void RenamesATagInEveryFileThatUsesItAndSaysWhichFilesChanged()
    {
        _builder.Build();

        _builder.Catalog.Rename("Music.Drums", "Music.Beats", out string? why).Should().BeTrue(why);

        string scene = _builder.ReadFile("Main.fvscene");
        scene.Should().Contain("\"Music.Beats\"").And.Contain("\"Music.Beats.Cellar\"").And.NotContain("Music.Drums");
        _builder.Rewritten.Should().Equal("Main.fvscene");
        _builder.ReadFile("Engine/Skip.fvscene").Should().Contain("Music.Drums");
    }

    [Fact]
    public void RemovesATagFromEveryFile()
    {
        _builder.Build();

        _builder.Catalog.Remove("Music", out string? why).Should().BeTrue(why);

        _builder.ReadFile("Main.fvscene").Should().NotContain("Music").And.Contain("Character.Fox");
    }

    [Fact]
    public void RefusesToRewriteFilesWhileThereIsUnsavedWork()
    {
        _builder.Busy("Save your work first: renaming a tag rewrites the files that use it.");
        _builder.Build();

        _builder.Catalog.Rename("Music.Drums", "Music.Beats", out string? why).Should().BeFalse();

        why.Should().StartWith("Save your work first");
        _builder.ReadFile("Main.fvscene").Should().Contain("Music.Drums");
        _builder.Catalog.Contains("Music.Drums").Should().BeTrue();
    }

    public void Dispose() => _builder.Dispose();
}
