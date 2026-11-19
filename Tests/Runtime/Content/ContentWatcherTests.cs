using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

public sealed class ContentWatcherTests : IDisposable
{
    private readonly ContentWatcherTestsBuilder _builder = new();

    public void Dispose()
    {
        _builder.Dispose();
    }

    [Fact]
    public void ReportsAnEditedFileWithinASecond()
    {
        _builder.WithFile("Moods/Day.json", "{}").Build();

        _builder.WriteFile("Moods/Day.json", """{ "ambient": 0.5 }""");
        IReadOnlyList<string> changed = _builder.UpdateUntilChanged(TimeSpan.FromSeconds(1));

        string fullPath = _builder.FullPath("Moods/Day.json");
        changed.Should().Equal(fullPath);
    }

    [Fact]
    public void ReportsSeveralWritesToOneFileOnce()
    {
        _builder.WithFile("Paper.png", "a").Build();

        _builder.WriteFile("Paper.png", "b");
        _builder.WriteFile("Paper.png", "c");
        _builder.WriteFile("Paper.png", "d");
        _builder.UpdateUntilChanged(TimeSpan.FromSeconds(1));
        IReadOnlyList<string> changed = _builder.UpdateFor(TimeSpan.FromMilliseconds(300));

        changed.Should().ContainSingle("the writes came in quick succession");
    }

    [Fact]
    public void ReportsAFileSavedThroughARename()
    {
        _builder.WithFile("Room.scene.json", "{}").Build();

        _builder.WriteFile("Room.scene.json.tmp", """{ "entities": [] }""");
        _builder.ReplaceFile("Room.scene.json.tmp", "Room.scene.json");
        _builder.UpdateUntilChanged(TimeSpan.FromSeconds(1));

        string fullPath = _builder.FullPath("Room.scene.json");
        _builder.Changed.Should().Contain(fullPath);
    }
}
