using System.Numerics;

using FlickerVoid.Engine.Platform.Input;

using Silk.NET.Input;

namespace FlickerVoid.Engine.Platform.Tests.Input;

/// <summary>Builds the input state under test as a previous frame left it: keys and buttons held, the mouse somewhere.</summary>
internal sealed class InputStateTestsBuilder
{
    private readonly List<Key> _keys = [];
    private readonly List<MouseButton> _buttons = [];
    private Vector2 _mousePosition;

    public InputStateTestsBuilder WithKeyHeld(Key key)
    {
        _keys.Add(key);
        return this;
    }

    public InputStateTestsBuilder WithButtonHeld(MouseButton button)
    {
        _buttons.Add(button);
        return this;
    }

    public InputStateTestsBuilder WithMouseAt(Vector2 position)
    {
        _mousePosition = position;
        return this;
    }

    public InputState Build()
    {
        InputState input = new();
        foreach (Key key in _keys)
        {
            input.PressKey(key);
        }

        foreach (MouseButton button in _buttons)
        {
            input.PressButton(button);
        }

        input.MoveMouse(_mousePosition);
        input.EndFrame();
        return input;
    }
}
