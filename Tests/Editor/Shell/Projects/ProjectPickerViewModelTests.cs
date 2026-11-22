using FlickerVoid.Editor.Persistence.Projects;
using FlickerVoid.Editor.Projects;

namespace FlickerVoid.Editor.Tests.Projects;

public sealed class ProjectPickerViewModelTests : IDisposable
{
    private readonly ProjectPickerViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ListsTheProjectsInTheProjectsFolderByName()
    {
        ProjectPickerViewModel picker = _builder.WithProject("Hollow").WithProject("Abbey").Build();

        picker.Projects.Select(static project => project.Name).Should().Equal("Abbey", "Hollow");
    }

    [Fact]
    public void ListsTheProjectsOpenedBeforeFromOtherFoldersToo()
    {
        string tower = _builder.WithRememberedProject("Tower");
        ProjectPickerViewModel picker = _builder.WithProject("Hollow").WithProject("Abbey").WithOpenedBefore("Hollow").Build();

        picker.Projects.Select(static project => project.Name).Should().Equal("Abbey", "Hollow", "Tower");
        picker.Projects[2].File.Should().Be(tower);
    }

    [Fact]
    public void SelectsTheProjectOpenedLast()
    {
        _builder.WithRememberedProject("Tower");
        ProjectPickerViewModel picker = _builder.WithProject("Hollow").WithOpenedBefore("Hollow").Build();

        picker.SelectedProject!.Name.Should().Be("Hollow");
    }

    [Fact]
    public async Task RemembersTheProjectItOpensForTheNextRun()
    {
        ProjectPickerViewModel picker = _builder.WithProject("Hollow").Build();
        string file = picker.Projects[0].File;

        await picker.OpenFile(file);

        _builder.Recent.Files.Should().Equal(file);
    }

    [Fact]
    public async Task OpeningAProjectBuildsItAndRaisesOpened()
    {
        ProjectPickerViewModel picker = _builder.WithProject("Hollow").Build();
        EditorProject? opened = null;
        picker.Opened += project => opened = project;

        await picker.OpenCommand.ExecuteAsync(picker.Projects[0]);

        opened!.Name.Should().Be("Hollow");
        picker.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task AFailedBuildShowsItsOutput()
    {
        ProjectPickerViewModel picker = _builder.WithProject("Hollow").WithFailingBuild("error CS1002").Build();
        bool opened = false;
        picker.Opened += _ => opened = true;

        await picker.OpenCommand.ExecuteAsync(picker.Projects[0]);

        opened.Should().BeFalse();
        picker.Status.Should().Contain("error CS1002");
        _builder.Recent.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task CreatesANewProjectFromTheTemplateAndOpensIt()
    {
        ProjectPickerViewModel picker = _builder.Build();
        EditorProject? opened = null;
        picker.Opened += project => opened = project;
        picker.NewName = "Hollow";

        await picker.CreateCommand.ExecuteAsync(null);

        _builder.Created.Should().Equal("Hollow");
        opened!.Name.Should().Be("Hollow");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Two Words")]
    [InlineData("9Lives")]
    public async Task RefusesANameThatCantNameAProject(string name)
    {
        ProjectPickerViewModel picker = _builder.Build();
        picker.NewName = name;

        await picker.CreateCommand.ExecuteAsync(null);

        _builder.Created.Should().BeEmpty();
        picker.Status.Should().NotBeEmpty();
    }
}
