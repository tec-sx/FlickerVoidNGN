using FlickerVoid.Editor.Documents;

namespace FlickerVoid.Editor.Tests.Documents;

/// <summary>The document tabs over the viewport: editors open in place beside the level, one tab per file.</summary>
public sealed class DocumentTabsTests
{
    private readonly DocumentTabs _tabs = DocumentTabsTestsBuilder.Build();

    [Fact]
    public void TheLevelIsShownUntilADocumentOpens()
    {
        bool before = _tabs.IsLevelShown;

        EditorDocument graph = DocumentTabsTestsBuilder.Document("Quests/Cellar.fvflow");
        _tabs.Show(graph);

        before.Should().BeTrue();
        _tabs.IsLevelShown.Should().BeFalse();
        _tabs.Selected.Should().BeSameAs(graph);
        _tabs.HasDocuments.Should().BeTrue();
    }

    [Fact]
    public void ShowingAFileAlreadyOpenSelectsItsTabAndKeepsOne()
    {
        EditorDocument first = DocumentTabsTestsBuilder.Document("Quests/Cellar.fvflow");
        EditorDocument other = DocumentTabsTestsBuilder.Document("Dialogues/Innkeeper.fvflow");
        _tabs.Show(first);
        _tabs.Show(other);

        EditorDocument again = DocumentTabsTestsBuilder.Document("quests/cellar.fvflow");
        EditorDocument shown = _tabs.Show(again);

        shown.Should().BeSameAs(first);
        _tabs.Documents.Should().HaveCount(2);
        _tabs.Selected.Should().BeSameAs(first);
    }

    [Fact]
    public void ClosingTheSelectedTabForgetsItAndShowsTheLevel()
    {
        bool closed = false;
        EditorDocument graph = DocumentTabsTestsBuilder.Document("Quests/Cellar.fvflow", () => closed = true);
        _tabs.Show(graph);

        _tabs.CloseCommand.Execute(graph);

        closed.Should().BeTrue();
        _tabs.Documents.Should().BeEmpty();
        _tabs.IsLevelShown.Should().BeTrue();
        _tabs.HasDocuments.Should().BeFalse();
    }

    [Fact]
    public void TheLevelTabShowsTheViewportAgainAndKeepsTheDocumentsOpen()
    {
        EditorDocument graph = DocumentTabsTestsBuilder.Document("Quests/Cellar.fvflow");
        _tabs.Show(graph);

        _tabs.ShowLevelCommand.Execute(null);

        _tabs.IsLevelShown.Should().BeTrue();
        _tabs.Documents.Should().ContainSingle();
    }
}
