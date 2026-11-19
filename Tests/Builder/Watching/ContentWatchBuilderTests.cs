namespace FlickerVoid.Engine.Builder.Tests.Watching;

public sealed class ContentWatchBuilderTests : IDisposable
{
    private readonly ContentWatchBuilderTestsBuilder _builder = new();

    public void Dispose()
    {
        _builder.Dispose();
    }

    [Fact]
    public void RebuildsASavedSourceFileWithinASecond()
    {
        _builder.WithSource("Notes/Hello.txt", "hello").Build();

        _builder.WriteSource("Notes/Hello.txt", "hello again");
        bool built = _builder.UpdateUntilBuilt(TimeSpan.FromSeconds(1));

        built.Should().BeTrue();
        _builder.ReadOutput("Notes/Hello.upper").Should().Be("HELLO AGAIN");
    }

    [Fact]
    public void BuildsNothingWhileNoSourceChanges()
    {
        _builder.WithSource("Notes/Hello.txt", "hello").Build();

        bool built = _builder.UpdateUntilBuilt(TimeSpan.FromMilliseconds(300));

        built.Should().BeFalse();
        _builder.Importer.Imported.Should().Equal("Notes/Hello.txt");
    }
}
