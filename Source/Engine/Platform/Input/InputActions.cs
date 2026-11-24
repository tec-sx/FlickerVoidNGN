using System.Numerics;

using Arch.System;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Platform.Input;

/// <summary>
/// Reads the input map's actions from the keyboard and mouse once a frame, like Enhanced Input: each action's value, whether it is
/// held, and an <see cref="InputActionEvent"/> when it is pressed or released. Systems read actions by tag, never keys. While the game
/// holds contexts (<see cref="Enter"/>), only the innermost one's actions are read; the rest read as up, so a held one is released.
/// </summary>
public sealed class InputActions : ISystem<GameTime>
{
    private readonly IInputState _input;
    private readonly EventBus _events;
    private readonly Dictionary<Tag, ActionState> _actions = [];
    private readonly List<Tag> _contexts = [];
    private readonly HashSet<Tag> _read = [];
    private readonly List<Tag> _stale = [];

    public InputActions(IInputState input, EventBus events, InputMap map)
    {
        _input = input;
        _events = events;
        Map = map;
    }

    /// <summary>The actions read; changing it, such as after the map's file changed, takes effect on the next frame.</summary>
    public InputMap Map { get; set; }

    /// <summary>Overlays that take the mouse's buttons: while any captures the pointer, button bindings read as up.</summary>
    public List<IPointerCapture> PointerCaptures { get; } = [];

    /// <summary>Overlays that take the keyboard, such as a console: while any captures it, key bindings read as up.</summary>
    public List<IKeyboardCapture> KeyboardCaptures { get; } = [];

    /// <summary>The innermost context the game holds, or none.</summary>
    public Tag Context
    {
        get
        {
            if (_contexts.Count == 0)
            {
                return Tag.None;
            }

            return _contexts[^1];
        }
    }

    /// <summary>The contexts the game holds, outermost first.</summary>
    public IReadOnlyList<Tag> Contexts => _contexts;

    public bool IsDown(Tag action) => _actions.TryGetValue(action, out ActionState state) && state.Down;

    public bool WasPressed(Tag action) => _actions.TryGetValue(action, out ActionState state) && state.Pressed;

    public bool WasReleased(Tag action) => _actions.TryGetValue(action, out ActionState state) && state.Released;

    /// <summary>The action's value this frame: the sum of its bindings' values, such as a move vector or the mouse's movement.</summary>
    public Vector2 GetValue(Tag action)
    {
        if (_actions.TryGetValue(action, out ActionState state))
        {
            return state.Value;
        }

        return Vector2.Zero;
    }

    /// <summary>
    /// Holds a context, innermost from now until it is left or another is entered; entering one already held makes it innermost.
    /// The next frame reads only its actions (none for a context the map doesn't list).
    /// </summary>
    public void Enter(Tag context)
    {
        _contexts.Remove(context);
        _contexts.Add(context);
    }

    /// <summary>Lets the context go, wherever it is among those held; the next frame reads the actions of the one left innermost.</summary>
    public void Leave(Tag context)
    {
        _contexts.Remove(context);
    }

    public bool IsInContext(Tag context) => _contexts.Contains(context);

    /// <summary>Whether the innermost context reads the action; every action is read outside all contexts.</summary>
    public bool IsAllowed(Tag action)
    {
        if (_contexts.Count == 0)
        {
            return true;
        }

        InputContext? context = Map.FindContext(Context);
        return context is not null && context.Actions.Contains(action);
    }

    /// <summary>Ends every held action as cancelled, such as when the window loses focus or the game pauses.</summary>
    public void Cancel()
    {
        _stale.Clear();
        _stale.AddRange(_actions.Keys);
        foreach (Tag action in _stale)
        {
            if (_actions[action].Down)
            {
                _events.Send(new InputActionEvent(action, InputPhase.Cancelled));
            }

            _actions[action] = default;
        }
    }

    public void Initialize()
    {
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        bool pointerCaptured = IsPointerCaptured();
        bool keyboardCaptured = IsKeyboardCaptured();
        _read.Clear();
        foreach (InputActionDefinition definition in Map.Actions)
        {
            Read(definition, pointerCaptured, keyboardCaptured);
            _read.Add(definition.Id);
        }

        if (_actions.Count == _read.Count)
        {
            return;
        }

        _stale.Clear();
        foreach (Tag action in _actions.Keys)
        {
            if (!_read.Contains(action))
            {
                _stale.Add(action);
            }
        }

        foreach (Tag removed in _stale)
        {
            _actions.Remove(removed);
        }
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
    }

    private bool IsPointerCaptured()
    {
        foreach (IPointerCapture capture in PointerCaptures)
        {
            if (capture.CapturesPointer)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsKeyboardCaptured()
    {
        foreach (IKeyboardCapture capture in KeyboardCaptures)
        {
            if (capture.CapturesKeyboard)
            {
                return true;
            }
        }

        return false;
    }

    private void Read(InputActionDefinition definition, bool pointerCaptured, bool keyboardCaptured)
    {
        bool down = false;
        Vector2 value = Vector2.Zero;
        if (IsAllowed(definition.Id))
        {
            foreach (InputBinding binding in definition.Bindings)
            {
                bool captured = (pointerCaptured && binding.Button is not null) || (keyboardCaptured && binding.Key is not null);
                if (captured)
                {
                    continue;
                }

                down |= binding.IsDown(_input);
                value += binding.Read(_input);
            }
        }

        _actions.TryGetValue(definition.Id, out ActionState previous);
        ActionState state = new(down, down && !previous.Down, !down && previous.Down, value);
        _actions[definition.Id] = state;
        if (state.Pressed)
        {
            _events.Send(new InputActionEvent(definition.Id, InputPhase.Pressed));
        }

        if (state.Released)
        {
            _events.Send(new InputActionEvent(definition.Id, InputPhase.Released));
        }
    }

    private readonly record struct ActionState(bool Down, bool Pressed, bool Released, Vector2 Value);
}
