using System.Numerics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Ui.Surfaces;
using FlickerVoid.Interface.Hud;

using FlickerVoid.Interface.Hud.Conversations;

using Button = Avalonia.Controls.Button;
using MouseButton = Silk.NET.Input.MouseButton;

namespace FlickerVoid.Interface.Tests.Hud;

public sealed class HudViewTests : IDisposable
{
    private const int Width = 1280;
    private const int Height = 720;

    private readonly HudViewModelTestsBuilder _builder = new();
    private readonly InputState _input = new();
    private UiSurface? _surface;

    public void Dispose()
    {
        _surface?.Dispose();
        _builder.Dispose();
    }

    [Fact]
    public void TheDialogueBoxTakesClicksAndTheRestOfTheHudLeavesThemToTheWorld()
    {
        UiSurface surface = Show();
        _builder.StartQuest("Quest.Cellar");
        _builder.Talk();

        Point(Width / 2, Height - 60, surface);
        bool overDialogue = surface.CapturesPointer;
        Point(100, 50, surface);
        bool overQuests = surface.CapturesPointer;

        overDialogue.Should().BeTrue();
        overQuests.Should().BeFalse("the quest tracker only shows");
    }

    [Fact]
    public void ClickingAChoicePicksIt()
    {
        UiSurface surface = Show();
        _builder.Talk();
        HudViewModel hud = (HudViewModel)surface.Content.DataContext!;
        hud.Dialogue.AdvanceCommand.Execute(null);
        _builder.Game.Events.Dispatch();
        surface.Update(_input);

        Button first = surface.Content.GetVisualDescendants().OfType<Button>().First(button => button.DataContext is DialogueChoiceViewModel);
        Avalonia.Point centre = first.TranslatePoint(new Avalonia.Point(first.Bounds.Width / 2, first.Bounds.Height / 2), surface.Content)!.Value;
        Point((float)centre.X, (float)centre.Y, surface);
        _input.PressButton(MouseButton.Left);
        surface.Update(_input);
        _input.EndFrame();
        _input.ReleaseButton(MouseButton.Left);
        surface.Update(_input);
        _builder.Game.Events.Dispatch();

        hud.Dialogue.Line.Should().Be("Find my key.");
    }

    private UiSurface Show()
    {
        UiPlatform.Start<InterfaceApplication>();
        HudViewModel hud = _builder.Build();
        HudView view = new() { DataContext = hud };
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
