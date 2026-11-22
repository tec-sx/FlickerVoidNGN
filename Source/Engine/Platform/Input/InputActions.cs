using System.Numerics;

using Arch.System;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Platform.Input;

/// <summary>
/// Reads the input map's actions from the keyboard and mouse once a frame, like Enhanced Input: each action's value, whether it is
/// held, and an <see cref="InputActionEvent"/> when it is pressed or released. Systems read actions by tag, never keys.
/// </summary>
public sealed class InputActions : ISystem<GameTime>
{
    private readonly IInputState _input;
    private readonly EventBus _events;
    private readonly Dictionary<Tag, ActionState> _actions = [];

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

    /// <summary>Ends every held action as cancelled, such as when the window loses focus or the game pauses.</summary>
    public void Cancel()
    {
        foreach ((Tag action, ActionState state) in _actions.ToList())
        {
            if (state.Down)
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
        HashSet<Tag> read = [];
        bool captured = PointerCaptures.Any(capture => capture.CapturesPointer);
        foreach (InputActionDefinition definition in Map.Actions)
        {
            Read(definition, captured);
            read.Add(definition.Id);
        }

        foreach (Tag removed in _actions.Keys.Where(action => !read.Contains(action)).ToList())
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

    private void Read(InputActionDefinition definition, bool pointerCaptured)
    {
        bool down = false;
        Vector2 value = Vector2.Zero;
        foreach (InputBinding binding in definition.Bindings)
        {
            if (pointerCaptured && binding.Button is not null)
            {
                continue;
            }

            down |= binding.IsDown(_input);
            value += binding.Read(_input);
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
