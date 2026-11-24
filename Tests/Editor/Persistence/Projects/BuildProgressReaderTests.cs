using FlickerVoid.Editor.Persistence.Projects;

namespace FlickerVoid.Editor.Persistence.Tests.Projects;

public sealed class BuildProgressReaderTests
{
    private readonly BuildProgressReaderTestsBuilder _builder = new();

    [Fact]
    public void CountsTheProjectsBuiltOfThoseTheBuildBuilds()
    {
        BuildProgressReader reader = _builder.WithProject("Engine").WithProject("Game", "Engine").Build();

        BuildProgress progress = reader.Read("  Engine -> /engine/bin/Engine.dll");

        progress.Built.Should().Be(1);
        progress.Total.Should().Be(2);
    }

    [Fact]
    public void ShowsTheProjectsWhoseReferencesAreBuiltAsBuildingNow()
    {
        BuildProgressReader reader = _builder
            .WithProject("Engine")
            .WithProject("Physics", "Engine")
            .WithProject("Story", "Engine")
            .WithProject("Game", "Physics", "Story")
            .Build();

        BuildProgress progress = reader.Read("  Engine -> /engine/bin/Engine.dll");

        progress.Status.Should().Be("Building Physics, Story");
    }

    [Fact]
    public void CountsAProjectBuiltTwiceOnce()
    {
        BuildProgressReader reader = _builder.WithProject("Engine").WithProject("Game", "Engine").Build();
        reader.Read("  Engine -> /engine/bin/net10.0/Engine.dll");

        BuildProgress progress = reader.Read("  Engine -> /engine/bin/netstandard2.0/Engine.dll");

        progress.Built.Should().Be(1);
    }

    [Fact]
    public void ShowsTheBuildsOtherLinesAsTheyAre()
    {
        BuildProgressReader reader = _builder.WithProject("Game").Build();

        BuildProgress progress = reader.Read("  Content: 53 built, 0 up to date, 0 removed, 0 failed.");

        progress.Should().Be(new BuildProgress(0, 1, "Content: 53 built, 0 up to date, 0 removed, 0 failed."));
    }

    [Fact]
    public void KeepsItsStatusThroughBlankLinesAndTheSummaryOnceEveryProjectIsBuilt()
    {
        BuildProgressReader reader = _builder.WithProject("Game").Build();
        reader.Read("  Game -> /game/bin/Game.dll");

        reader.Read("");
        BuildProgress progress = reader.Read("    0 Warning(s)");

        progress.Should().Be(new BuildProgress(1, 1, "Built Game"));
    }

    [Fact]
    public void ReadsTheProjectsAndTheirReferencesFromTheRestoreGraph()
    {
        string graph = _builder.WithProject("Engine").WithProject("Game", "Engine").RestoreGraph();
        BuildProgressReader reader = BuildProgressReader.FromRestoreGraph(graph);

        BuildProgress progress = reader.Read("  Engine -> /engine/bin/Engine.dll");

        progress.Should().Be(new BuildProgress(1, 2, "Building Game"));
    }

    [Fact]
    public void WithoutItsProjectsTheBuildHasNoTotalAndShowsItsLinesAsTheyAre()
    {
        BuildProgressReader reader = _builder.Build();

        BuildProgress progress = reader.Read("  Engine -> /engine/bin/Engine.dll");

        progress.Should().Be(new BuildProgress(0, 0, "Engine -> /engine/bin/Engine.dll"));
    }
}
