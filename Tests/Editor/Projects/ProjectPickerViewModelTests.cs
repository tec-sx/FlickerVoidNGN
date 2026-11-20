using FlickerVoid.Engine.Editor.Projects;

namespace FlickerVoid.Engine.Editor.Tests.Projects;

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
