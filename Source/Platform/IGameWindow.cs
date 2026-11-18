using FlickerVoid.Engine.Platform.Input;

namespace FlickerVoid.Engine.Platform;

/// <summary>The game's window: runs the game loop until it closes and provides the frame's keyboard and mouse input.</summary>
public interface IGameWindow
{
    IInputState Input { get; }

    string Title { get; set; }

    /// <summary>Runs until the window closes: each frame handles the window's events, ticks the game with the real time since the last frame, then ends the input frame.</summary>
    void Run();

    void Close();
}
