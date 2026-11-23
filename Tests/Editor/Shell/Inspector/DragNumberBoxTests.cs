using Avalonia.Input;

namespace FlickerVoid.Editor.Tests.Inspector;

public sealed class DragNumberBoxTests
{
    private readonly DragNumberBoxTestsBuilder _builder = new();

    [Fact]
    public void CopiesTheValueWithCtrlCAfterAClick()
    {
        _builder.Run(async () =>
        {
            await _builder.SetClipboard("old");

            _builder.Click(_builder.First, MouseButton.Left);
            _builder.PressCopy();

            string? copied = await _builder.GetClipboard();
            copied.Should().Be("1.5");
        });
    }

    [Fact]
    public void SelectsTheValueOnARightClickSoTheMenuCanCopyIt()
    {
        _builder.Run(() =>
        {
            _builder.First.SelectedText.Should().BeEmpty("nothing is selected before the click");
            _builder.Click(_builder.First, MouseButton.Right);

            _builder.First.SelectedText.Should().Be("1.5");
            _builder.First.CanCopy.Should().BeTrue();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void CopiesTheWholeValueWhenNothingIsSelected()
    {
        _builder.Run(async () =>
        {
            await _builder.SetClipboard("old");
            _builder.Click(_builder.Second, MouseButton.Left);
            _builder.Second.ClearSelection();

            _builder.PressCopy();

            string? copied = await _builder.GetClipboard();
            copied.Should().Be("7");
        });
    }
}
