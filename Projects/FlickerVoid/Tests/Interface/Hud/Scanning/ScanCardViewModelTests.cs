using FlickerVoid.Interface.Hud.Scanning;

namespace FlickerVoid.Interface.Tests.Hud.Scanning;

public sealed class ScanCardViewModelTests : IDisposable
{
    private readonly ScanCardViewModelTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsOnlyInScanMode()
    {
        ScanCardViewModel card = _builder.Build();

        card.Update();
        bool hidden = card.IsVisible;
        _builder.Scanner.SetScanMode(_builder.Protagonist, true);
        _builder.Run(0.2f);
        card.Update();

        hidden.Should().BeFalse();
        card.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void ShowsTheFocusAndHowFarItsScanHasCome()
    {
        ScanCardViewModel card = _builder.Build();
        _builder.Scanner.SetScanMode(_builder.Protagonist, true);
        _builder.Run(0.2f);

        _builder.Scanner.StartScan(_builder.Protagonist);
        _builder.Run(0.5f);
        card.Update();

        card.HasFocus.Should().BeTrue();
        card.Title.Should().Be("Old crate");
        card.Progress.Should().BeInRange(40, 60);
        card.IsScanned.Should().BeFalse();
        card.Entries.Should().BeEmpty();
    }

    [Fact]
    public void ListsAtMostThreeEntriesOnceScanned()
    {
        ScanCardViewModel card = _builder.Build();
        _builder.Scanner.SetScanMode(_builder.Protagonist, true);
        _builder.Run(0.2f);

        _builder.Scanner.StartScan(_builder.Protagonist);
        _builder.Run(1.2f);
        card.Update();

        card.IsScanned.Should().BeTrue();
        card.Entries.Select(entry => entry.Label).Should().Equal("Wood", "Marks", "Smell");
        card.Entries[0].Text.Should().Be("Old oak.");
    }
}
