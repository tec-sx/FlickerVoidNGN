using System.Numerics;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Platform.Input;

using Silk.NET.Input;

namespace FlickerVoid.Engine.Platform.Tests.Input;

public sealed class InputActionsTests : IDisposable
{
    private const string Map = """
        {
          "type": "inputMap",
          "actions": [
            {
              "id": "Input.Move",
              "bindings": [
                { "key": "W", "value": [0, 1] }, { "key": "S", "value": [0, -1] },
                { "key": "A", "value": [-1, 0] }, { "key": "D", "value": [1, 0] }
              ]
            },
            { "id": "Input.Look", "bindings": [ { "mouseMovement": true, "value": [0.5, -0.5] } ] },
            { "id": "Input.Interact", "bindings": [ { "key": "E" }, { "button": "Left" } ] }
          ],
          "contexts": [
            { "id": "Input.Context.Menu", "actions": [ "Input.Interact" ] }
          ]
        }
        """;

    private static readonly Tag MenuContext = new("Input.Context.Menu");
    private static readonly Tag UnknownContext = new("Input.Context.Unknown");

    private static readonly Tag Move = new("Input.Move");
    private static readonly Tag Look = new("Input.Look");
    private static readonly Tag Interact = new("Input.Interact");

    private readonly InputActionsTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void AddsTheValuesOfTheBindingsThatAreDown()
    {
        InputActions actions = _builder.Build(Map);
        _builder.Input.PressKey(Key.W);
        _builder.Input.PressKey(Key.D);

        _builder.Frame(actions);

        Vector2 expected = new(1, 1);
        actions.GetValue(Move).Should().Be(expected);
        actions.IsDown(Move).Should().BeTrue();
    }

    [Fact]
    public void ScalesTheMousesMovement()
    {
        InputActions actions = _builder.Build(Map);
        _builder.Input.MoveMouse(new Vector2(10, 10));
        _builder.Input.EndFrame();
        _builder.Input.MoveMouse(new Vector2(14, 12));

        _builder.Frame(actions);

        Vector2 expected = new(2, -1);
        actions.GetValue(Look).Should().Be(expected);
    }

    [Fact]
    public void SendsPressedOnceAndReleasedWhenTheLastBindingComesUp()
    {
        InputActions actions = _builder.Build(Map);
        _builder.Input.PressKey(Key.E);
        _builder.Frame(actions);
        bool pressed = actions.WasPressed(Interact);
        _builder.Input.PressButton(MouseButton.Left);
        _builder.Input.ReleaseKey(Key.E);
        _builder.Frame(actions);
        bool pressedAgain = actions.WasPressed(Interact);
        _builder.Input.ReleaseButton(MouseButton.Left);

        _builder.Frame(actions);

        pressed.Should().BeTrue();
        pressedAgain.Should().BeFalse("the button went down while the key was still held");
        actions.WasReleased(Interact).Should().BeTrue();
        _builder.Events.Should().Equal(new InputActionEvent(Interact, InputPhase.Pressed), new InputActionEvent(Interact, InputPhase.Released));
    }

    [Fact]
    public void CancelsHeldActions()
    {
        InputActions actions = _builder.Build(Map);
        _builder.Input.PressKey(Key.E);
        _builder.Frame(actions);

        actions.Cancel();
        _builder.Bus.Dispatch();

        actions.IsDown(Interact).Should().BeFalse();
        _builder.Events.Should().EndWith(new InputActionEvent(Interact, InputPhase.Cancelled));
    }

    [Fact]
    public void MouseButtonsOverAnInterfaceThatCapturesThePointerAreLeftToIt()
    {
        InputActions actions = _builder.Build(Map);
        IPointerCapture overlay = Substitute.For<IPointerCapture>();
        overlay.CapturesPointer.Returns(true);
        actions.PointerCaptures.Add(overlay);
        _builder.Input.PressButton(MouseButton.Left);
        _builder.Frame(actions);
        bool clickedThrough = actions.IsDown(Interact);
        _builder.Input.PressKey(Key.E);

        _builder.Frame(actions);

        clickedThrough.Should().BeFalse();
        actions.IsDown(Interact).Should().BeTrue("keys still reach the game");
    }

    [Fact]
    public void AContextLetsOnlyItsOwnActionsThrough()
    {
        InputActions actions = _builder.Build(Map);
        actions.Enter(MenuContext);
        _builder.Input.PressKey(Key.W);
        _builder.Input.PressKey(Key.E);

        _builder.Frame(actions);

        actions.IsDown(Move).Should().BeFalse();
        actions.GetValue(Move).Should().Be(Vector2.Zero);
        actions.IsDown(Interact).Should().BeTrue();
    }

    [Fact]
    public void EnteringAContextReleasesTheHeldActionsItBlocks()
    {
        InputActions actions = _builder.Build(Map);
        _builder.Input.PressKey(Key.W);
        _builder.Frame(actions);

        actions.Enter(MenuContext);
        _builder.Frame(actions);

        actions.WasReleased(Move).Should().BeTrue();
        _builder.Events.Should().EndWith(new InputActionEvent(Move, InputPhase.Released));
    }

    [Fact]
    public void LeavingTheInnermostContextGoesBackToTheOneUnderIt()
    {
        InputActions actions = _builder.Build(Map);
        actions.Enter(MenuContext);
        actions.Enter(UnknownContext);
        _builder.Input.PressKey(Key.E);
        _builder.Frame(actions);
        bool blocked = !actions.IsDown(Interact);

        actions.Leave(UnknownContext);
        _builder.Frame(actions);

        blocked.Should().BeTrue("a context the map doesn't list lets nothing through");
        actions.Context.Should().Be(MenuContext);
        actions.IsDown(Interact).Should().BeTrue();
    }

    [Fact]
    public void LeavingEveryContextLetsEveryActionThroughAgain()
    {
        InputActions actions = _builder.Build(Map);
        actions.Enter(MenuContext);
        _builder.Input.PressKey(Key.W);
        _builder.Frame(actions);

        actions.Leave(MenuContext);
        _builder.Frame(actions);

        actions.Context.IsValid.Should().BeFalse();
        actions.IsDown(Move).Should().BeTrue();
        actions.IsInContext(MenuContext).Should().BeFalse();
    }

    [Fact]
    public void KeysUnderAKeyboardCaptureAreLeftToIt()
    {
        InputActions actions = _builder.Build(Map);
        IKeyboardCapture console = Substitute.For<IKeyboardCapture>();
        console.CapturesKeyboard.Returns(true);
        actions.KeyboardCaptures.Add(console);
        _builder.Input.PressKey(Key.W);
        _builder.Input.PressKey(Key.E);
        _builder.Frame(actions);
        bool typedThrough = actions.IsDown(Move) || actions.IsDown(Interact);
        _builder.Input.PressButton(MouseButton.Left);

        _builder.Frame(actions);

        typedThrough.Should().BeFalse();
        actions.IsDown(Interact).Should().BeTrue("mouse buttons still reach the game");
    }
}
