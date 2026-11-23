using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Persistence.Tests.Projects;

public sealed class RecentProjectsTests : IDisposable
{
    private readonly RecentProjectsTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void RemembersOpenedProjectsForTheNextRunNewestFirst()
    {
        string hollow = _builder.WithProject("Hollow");
        string abbey = _builder.WithProject("Abbey");
        RecentProjects recent = _builder.Build();

        recent.Add(hollow);
        recent.Add(abbey);

        _builder.Build().Files.Should().Equal(abbey, hollow);
    }

    [Fact]
    public void OpeningAProjectAgainMovesItToTheFront()
    {
        string hollow = _builder.WithProject("Hollow");
        string abbey = _builder.WithProject("Abbey");
        RecentProjects recent = _builder.Build();
        recent.Add(hollow);
        recent.Add(abbey);

        recent.Add(hollow);

        _builder.Build().Files.Should().Equal(hollow, abbey);
    }

    [Fact]
    public void KeepsOnlyTheNewestProjects()
    {
        RecentProjects recent = _builder.Build();
        List<string> files = [];
        for (int index = 0; index < RecentProjects.Limit + 2; index++)
        {
            string file = _builder.WithProject("Game" + index);
            files.Add(file);
            recent.Add(file);
        }

        files.Reverse();
        _builder.Build().Files.Should().Equal(files.Take(RecentProjects.Limit));
    }

    [Fact]
    public void ForgetsProjectsThatAreGone()
    {
        string hollow = _builder.WithProject("Hollow");
        _builder.Build().Add(hollow);

        File.Delete(hollow);

        _builder.Build().Files.Should().BeEmpty();
    }

    [Fact]
    public void AMissingOrBrokenFileRemembersNothing()
    {
        _builder.Build().Files.Should().BeEmpty();

        _builder.WithBrokenFile();

        _builder.Build().Files.Should().BeEmpty();
    }
}
