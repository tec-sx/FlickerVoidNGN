using System.Numerics;

using Silk.NET.Input;

namespace FlickerVoid.Engine.Platform.Input;

/// <summary>Keyboard and mouse state for the current frame, as systems read it; presses, releases, mouse movement and scrolling last until the frame ends.</summary>
/// <remarks>Read presses in frame systems: a frame can run no fixed step or several, so fixed systems would miss or repeat them.</remarks>
public interface IInputState
{
    Vector2 MousePosition { get; }

    Vector2 MouseDelta { get; }

    /// <summary>Wheel movement this frame; positive scrolls away from the player.</summary>
    float ScrollDelta { get; }

    /// <summary>The characters typed this frame, in order, as the keyboard layout produced them.</summary>
    string TypedText { get; }

    bool IsDown(Key key);

    bool WasPressed(Key key);

    bool WasReleased(Key key);

    bool IsDown(MouseButton button);

    bool WasPressed(MouseButton button);

    bool WasReleased(MouseButton button);
}
