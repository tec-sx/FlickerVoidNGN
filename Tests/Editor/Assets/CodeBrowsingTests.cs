namespace FlickerVoid.Editor.Assets.Tests;

public sealed class CodeBrowsingTests : IDisposable
{
    private const string JsonContext = """
        namespace Cellar.Gameplay;

        public sealed partial class CellarJsonContext : JsonSerializerContext;
        """;

    private readonly AssetBrowserViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsTheCodeFolderAsASecondRootWithoutBuildOutput()
    {
        AssetBrowserViewModel browser = _builder
            .WithContentFile("Scenes/Room.fvscene")
            .WithCodeFile("Gameplay/Cellar.Gameplay.csproj")
            .WithCodeFile("Gameplay/Barrel.cs")
            .WithCodeFile("Gameplay/obj/Barrel.g.cs")
            .BuildWithCode();

        browser.Folders.Should().HaveCount(2);
        AssetFolder code = browser.Folders[1];
        code.Name.Should().Be("Code");
        code.Children.Should().ContainSingle().Which.Name.Should().Be("Gameplay");
        browser.SelectedFolder = code.Children[0];
        browser.Assets.Should().ContainSingle(static asset => asset.Type == AssetTypes.Code).Which.Name.Should().Be("Barrel");
        browser.GetFullPath(browser.Assets[0]).Should().EndWith(Path.Combine("_Code", "Gameplay", "Barrel.cs"));
    }

    [Fact]
    public void OffersAFlowNodeOnlyWithACodeFolder()
    {
        AssetBrowserViewModel withCode = _builder.BuildWithCode();
        AssetBrowserViewModel withoutCode = _builder.Build();

        withCode.NewChoices.Should().Contain(static choice => choice.Name == "Flow Node" && choice.Group == "Flow");
        withoutCode.NewChoices.Should().NotContain(static choice => choice.Name == "Flow Node");
    }

    [Fact]
    public void ANewFlowNodeInACodeFolderIsCreatedThere()
    {
        AssetBrowserViewModel browser = _builder.WithCodeFile("Gameplay/Cellar.Gameplay.csproj").WithCodeFile("Gameplay/World/Doors/Door.cs").BuildWithCode();
        browser.SelectedFolder = browser.Folders[1].Children[0].Children[0].Children[0];

        browser.NewFlowNode();

        browser.Prompt!.Folder.Should().Be("Gameplay/World/Doors");
        browser.Prompt.Folders.Should().Equal("Gameplay", "Gameplay/World", "Gameplay/World/Doors");
    }

    [Fact]
    public void ANewFlowNodeFromContentSuggestsTheCodeFolderOfTheSameName()
    {
        AssetBrowserViewModel browser = _builder
            .WithContentFile("World/Doors/Cellar.fvflow")
            .WithCodeFile("Gameplay/Cellar.Gameplay.csproj")
            .WithCodeFile("Gameplay/World/Doors/Door.cs")
            .WithCodeFile("Gameplay/Protagonist/Hero.cs")
            .BuildWithCode();
        browser.SelectedFolder = browser.Folders[0].Children[0].Children[0];

        browser.NewFlowNode();

        browser.Prompt!.Folder.Should().Be("Gameplay/World/Doors");
    }

    [Fact]
    public void ANewFlowNodeFromAnUnmatchedFolderSuggestsTheFirstProject()
    {
        AssetBrowserViewModel browser = _builder
            .WithContentFile("Moods/Night.fvmood")
            .WithCodeFile("Audio/Notes.txt")
            .WithCodeFile("Gameplay/Cellar.Gameplay.csproj")
            .BuildWithCode();
        browser.SelectedFolder = browser.Folders[0].Children[0];

        browser.NewFlowNode();

        browser.Prompt!.Folder.Should().Be("Gameplay");
    }

    [Fact]
    public void ConfirmingThePromptWritesTheNodeAndSelectsIt()
    {
        AssetBrowserViewModel browser = _builder
            .WithCodeFile("Gameplay/Cellar.Gameplay.csproj")
            .WithCodeFile("Gameplay/CellarJsonContext.cs", JsonContext)
            .BuildWithCode();
        browser.NewFlowNode();
        browser.Prompt!.Name = "Open Gate";

        browser.ConfirmPrompt();

        browser.Prompt.Should().BeNull();
        _builder.CodeFileExists("Gameplay/OpenGateNode.cs").Should().BeTrue();
        browser.SelectedFolder!.Path.Should().Be("/Code/Gameplay");
        browser.SelectedAsset!.Name.Should().Be("OpenGateNode");
        browser.Status.Should().Contain("Build");
    }

    [Fact]
    public void KeepsThePromptAndSaysWhyWhenTheNodeCantBeWritten()
    {
        AssetBrowserViewModel browser = _builder.WithCodeFile("Gameplay/Cellar.Gameplay.csproj").BuildWithCode();
        browser.NewFlowNode();
        browser.Prompt!.Name = "Open Gate";

        browser.ConfirmPrompt();

        browser.Prompt.Should().NotBeNull();
        browser.Status.Should().Contain("JsonSerializerContext");
    }

    [Fact]
    public void LeavesCodeFilesToTheIde()
    {
        AssetBrowserViewModel browser = _builder.WithCodeFile("Gameplay/Barrel.cs").BuildWithCode();
        browser.SelectedFolder = browser.Folders[1].Children[0];
        browser.SelectedAsset = browser.Assets[0];

        browser.Delete();
        browser.Create(AssetTypes.Scene);

        _builder.CodeFileExists("Gameplay/Barrel.cs").Should().BeTrue();
        _builder.CodeFileExists("Gameplay/New Scene.fvscene").Should().BeFalse();
        browser.Status.Should().Contain("code");
    }
}
