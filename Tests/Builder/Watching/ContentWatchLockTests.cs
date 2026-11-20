using AwesomeAssertions;

using FlickerVoid.Engine.Builder.Watching;

using Xunit;

namespace FlickerVoid.Engine.Builder.Tests.Watching;

public sealed class ContentWatchLockTests
{
    [Fact]
    public void OnlyOneWatcherHoldsAManifest()
    {
        using ContentWatchLockTestsBuilder builder = new();

        using ContentWatchLock? first = builder.Build();
        using ContentWatchLock? second = builder.Build();

        first.Should().NotBeNull();
        second.Should().BeNull();
    }

    [Fact]
    public void TheManifestIsFreeAgainOnceTheWatcherLetsGo()
    {
        using ContentWatchLockTestsBuilder builder = new();
        ContentWatchLock? first = builder.Build();
        first!.Dispose();

        using ContentWatchLock? second = builder.Build();

        second.Should().NotBeNull();
    }
}
