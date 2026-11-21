using Silk.NET.Input;

namespace FlickerVoid.Engine.Debug.Tests;

public sealed class DebugUiTests : IDisposable
{
    private readonly DebugUiTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void DrawsNothingWhileHidden()
    {
        _builder.WithWindow("Hello", "Test", open: true);

        uint[] pixels = _builder.RenderFrame(visible: false);

        pixels.Should().OnlyContain(pixel => pixel == DebugUiTestsBuilder.Background);
    }

    [Fact]
    public void DrawsTheMainMenuBarAcrossTheTopWhenVisible()
    {
        _builder.WithWindow("Hello", "Test");

        uint[] pixels = _builder.RenderFrame(visible: true);

        uint topLeft = pixels[(2 * DebugUiTestsBuilder.Width) + (DebugUiTestsBuilder.Width - 2)];
        uint below = pixels[(DebugUiTestsBuilder.Height - 2) * DebugUiTestsBuilder.Width];
        topLeft.Should().NotBe(DebugUiTestsBuilder.Background);
        below.Should().Be(DebugUiTestsBuilder.Background);
    }

    [Fact]
    public void DrawsTextAndWindowsThatAreOpen()
    {
        _builder.WithWindow("Hello", "Test", open: true);
        uint[] closed = new DebugUiTestsBuilder().WithWindow("Hello", "Test").RenderAndDispose(visible: true, frames: 2);

        _builder.RenderFrame(visible: true);
        uint[] opened = _builder.RenderFrame(visible: true);

        int changed = opened.Where((pixel, index) => pixel != closed[index]).Count();
        changed.Should().BeGreaterThan(1000, "an open window covers part of the target");
    }

    [Fact]
    public void RunsAnOpenWindowsRenderMethodEachFrame()
    {
        _builder.WithWindow("Hello", "Test", open: true);

        _builder.RenderFrame(visible: true);
        _builder.RenderFrame(visible: true);

        _builder.Renders("Hello").Should().Be(2);
    }

    [Fact]
    public void DoesNotRenderWindowsThatAreClosed()
    {
        _builder.WithWindow("Hello", "Test");

        _builder.RenderFrame(visible: true);

        _builder.Renders("Hello").Should().Be(0);
    }

    [Fact]
    public void TogglesVisibilityWithF1()
    {
        _builder.WithWindow("Hello", "Test");
        _builder.Press(Key.F1);

        _builder.RenderFrame(visible: false);

        _builder.Ui.Visible.Should().BeTrue();
    }

    [Fact]
    public void ReportsWhenItWantsTheMouse()
    {
        _builder.WithWindow("Hello", "Test");
        _builder.MoveMouse(10, 5);

        _builder.RenderFrame(visible: true);
        _builder.RenderFrame(visible: true);

        _builder.Ui.WantsMouse.Should().BeTrue("the pointer is over the menu bar");
        _builder.Ui.CapturesPointer.Should().BeTrue();
    }

    [Fact]
    public void LeavesTheMouseToTheGameAwayFromItsWindows()
    {
        _builder.WithWindow("Hello", "Test");
        _builder.MoveMouse(DebugUiTestsBuilder.Width - 5, DebugUiTestsBuilder.Height - 5);

        _builder.RenderFrame(visible: true);
        _builder.RenderFrame(visible: true);

        _builder.Ui.WantsMouse.Should().BeFalse();
    }

    [Fact]
    public void RestoresWhichWindowsWereOpenFromItsLayoutFile()
    {
        _builder.WithWindow("Hello", "Test", open: true);
        _builder.RenderFrame(visible: true);
        _builder.Ui.SaveLayout();
        DebugUiTestsBuilder second = _builder.Another().WithWindow("Hello", "Test");

        second.RenderFrame(visible: true);

        second.Ui.Windows.Find("Hello")!.IsOpen.Should().BeTrue();
        second.Dispose();
    }
}
