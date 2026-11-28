using FlickerVoid.Editor.Core.Toasts;

namespace FlickerVoid.Editor.Core.Tests.Toasts;

public sealed class ToastsViewModelTests
{
    private readonly ToastsViewModelTestsBuilder _builder = new();

    [Fact]
    public void AWarningIsShownUntilItsTimeIsUp()
    {
        ToastsViewModel toasts = _builder.Build();

        toasts.Warn("Stairs need a storey above.");
        toasts.Update(ToastsViewModel.Seconds - 0.5f);
        int shown = toasts.Items.Count;
        toasts.Update(1);

        shown.Should().Be(1);
        toasts.Items.Should().BeEmpty();
    }

    [Fact]
    public void TheSameWarningAgainIsShownOnceAndStaysLonger()
    {
        ToastsViewModel toasts = _builder.Build();

        toasts.Warn("Click the wall it goes in.");
        toasts.Update(ToastsViewModel.Seconds - 0.5f);
        toasts.Warn("Click the wall it goes in.");
        toasts.Update(1);

        toasts.Items.Should().ContainSingle().Which.Message.Should().Be("Click the wall it goes in.");
    }

    [Fact]
    public void OnlyTheLatestFewAreShownTheNewestLast()
    {
        ToastsViewModel toasts = _builder.Build();

        for (int number = 1; number <= ToastsViewModel.Most + 1; number++)
        {
            toasts.Warn($"Warning {number}");
        }

        toasts.Items.Should().HaveCount(ToastsViewModel.Most);
        toasts.Items[0].Message.Should().Be("Warning 2");
        toasts.Items[^1].Message.Should().Be($"Warning {ToastsViewModel.Most + 1}");
    }

    [Fact]
    public void AToastIsDismissedByItsCommand()
    {
        ToastsViewModel toasts = _builder.Build();
        toasts.Warn("First");
        toasts.Warn("Second");

        toasts.Items[0].DismissCommand.Execute(null);

        toasts.Items.Should().ContainSingle().Which.Message.Should().Be("Second");
    }

    [Fact]
    public void AnEmptyWarningIsNotShown()
    {
        ToastsViewModel toasts = _builder.Build();

        toasts.Warn(" ");

        toasts.Items.Should().BeEmpty();
    }
}
