using System.Numerics;

using Silk.NET.Input;

namespace FlickerVoid.Engine.Platform.Input;

/// <summary>Input state the window writes as its events arrive and clears at the end of each frame.</summary>
public sealed class InputState : IInputState
{
    private readonly HashSet<Key> _keysDown = [];
    private readonly HashSet<Key> _keysPressed = [];
    private readonly HashSet<Key> _keysReleased = [];
    private readonly HashSet<MouseButton> _buttonsDown = [];
    private readonly HashSet<MouseButton> _buttonsPressed = [];
    private readonly HashSet<MouseButton> _buttonsReleased = [];

    public Vector2 MousePosition { get; private set; }

    public Vector2 MouseDelta { get; private set; }

    public float ScrollDelta { get; private set; }

    public bool IsDown(Key key)
    {
        return _keysDown.Contains(key);
    }

    public bool WasPressed(Key key)
    {
        return _keysPressed.Contains(key);
    }

    public bool WasReleased(Key key)
    {
        return _keysReleased.Contains(key);
    }

    public bool IsDown(MouseButton button)
    {
        return _buttonsDown.Contains(button);
    }

    public bool WasPressed(MouseButton button)
    {
        return _buttonsPressed.Contains(button);
    }

    public bool WasReleased(MouseButton button)
    {
        return _buttonsReleased.Contains(button);
    }

    /// <summary>Records a key going down; repeats while it is held don't count as new presses.</summary>
    public void PressKey(Key key)
    {
        if (_keysDown.Add(key))
        {
            _keysPressed.Add(key);
        }
    }

    public void ReleaseKey(Key key)
    {
        if (_keysDown.Remove(key))
        {
            _keysReleased.Add(key);
        }
    }

    public void PressButton(MouseButton button)
    {
        if (_buttonsDown.Add(button))
        {
            _buttonsPressed.Add(button);
        }
    }

    public void ReleaseButton(MouseButton button)
    {
        if (_buttonsDown.Remove(button))
        {
            _buttonsReleased.Add(button);
        }
    }

    public void MoveMouse(Vector2 position)
    {
        MouseDelta += position - MousePosition;
        MousePosition = position;
    }

    public void Scroll(float amount)
    {
        ScrollDelta += amount;
    }

    /// <summary>Releases every key and button, for when the window loses focus and would miss their releases.</summary>
    public void ReleaseAll()
    {
        _keysReleased.UnionWith(_keysDown);
        _keysDown.Clear();
        _buttonsReleased.UnionWith(_buttonsDown);
        _buttonsDown.Clear();
    }

    /// <summary>Forgets this frame's presses, releases, mouse movement and scrolling.</summary>
    public void EndFrame()
    {
        _keysPressed.Clear();
        _keysReleased.Clear();
        _buttonsPressed.Clear();
        _buttonsReleased.Clear();
        MouseDelta = Vector2.Zero;
        ScrollDelta = 0;
    }
}
