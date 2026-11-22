using System.Numerics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Ui.Surfaces;
using FlickerVoid.Interface.Hub;
using FlickerVoid.Interface.Hub.Journal;

using Button = Avalonia.Controls.Button;
using MouseButton = Silk.NET.Input.MouseButton;

namespace FlickerVoid.Interface.Tests.Hub;

public sealed class HubViewTests : IDisposable
{
    private const int Width = 1280;
    private const int Height = 720;

    private readonly HubViewModelTestsBuilder _builder = new();
    private readonly InputState _input = new();
    private UiSurface? _surface;

    public void Dispose()
    {
        _surface?.Dispose();
        _builder.Dispose();
    }

    [Fact]
    public void TakesEveryClickOnlyWhileOpen()
    {
        UiSurface surface = Show(out HubViewModel hub);

        Point(640, 400, surface);
        bool closed = surface.CapturesPointer;
        hub.Toggle();
        Point(640, 401, surface);
        bool open = surface.CapturesPointer;

        closed.Should().BeFalse();
        open.Should().BeTrue();
    }

    [Fact]
    public void ClickingATabShowsItsPage()
    {
        UiSurface surface = Show(out HubViewModel hub);
        hub.Toggle();
        surface.Update(_input);

        Button journal = surface.Content.GetVisualDescendants().OfType<Button>().First(button => button.DataContext is JournalPageViewModel);
        Avalonia.Point centre = journal.TranslatePoint(new Avalonia.Point(journal.Bounds.Width / 2, journal.Bounds.Height / 2), surface.Content)!.Value;
        Point((float)centre.X, (float)centre.Y, surface);
        _input.PressButton(MouseButton.Left);
        surface.Update(_input);
        _input.EndFrame();
        _input.ReleaseButton(MouseButton.Left);
        surface.Update(_input);

        hub.SelectedPage.Should().BeSameAs(hub.Journal);
    }

    private UiSurface Show(out HubViewModel hub)
    {
        UiPlatform.Start<InterfaceApplication>();
        hub = _builder.Build();
        HubView view = new() { DataContext = hub };
        _surface = new UiSurface(view, Width, Height);
        return _surface;
    }

    private void Point(float x, float y, UiSurface surface)
    {
        _input.MoveMouse(new Vector2(x, y));
        surface.Update(_input);
        _input.EndFrame();
    }
}
