using Arch.System;

using FlickerVoid.Engine.Platform;
using FlickerVoid.Engine.Runtime;

using Silk.NET.Input;

namespace FlickerVoid.Samples.Window;

/// <summary>Prints this frame's key presses and mouse clicks, shows the frame rate and mouse position in the window title, and closes the window on Escape or after a frame limit.</summary>
public sealed class InputEchoSystem(IGameWindow window, int frameLimit) : ISystem<GameTime>
{
    private static readonly TimeSpan TitleInterval = TimeSpan.FromSeconds(0.5);

    private TimeSpan _sinceTitle;
    private int _framesSinceTitle;

    public int Frames { get; private set; }

    public void Initialize()
    {
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        Frames++;
        PrintPresses();
        UpdateTitle(t.Elapsed);

        bool limitReached = frameLimit > 0 && Frames >= frameLimit;
        if (window.Input.WasPressed(Key.Escape) || limitReached)
        {
            window.Close();
        }
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
    }

    private void PrintPresses()
    {
        foreach (Key key in Enum.GetValues<Key>())
        {
            if (window.Input.WasPressed(key))
            {
                Console.WriteLine($"Key {key}");
            }
        }

        foreach (MouseButton button in Enum.GetValues<MouseButton>())
        {
            if (window.Input.WasPressed(button))
            {
                Console.WriteLine($"Mouse {button} at {window.Input.MousePosition}");
            }
        }
    }

    private void UpdateTitle(TimeSpan elapsed)
    {
        _sinceTitle += elapsed;
        _framesSinceTitle++;
        if (_sinceTitle < TitleInterval)
        {
            return;
        }

        double framesPerSecond = _framesSinceTitle / _sinceTitle.TotalSeconds;
        window.Title = $"FlickerVoid  {framesPerSecond:F0} fps  mouse {window.Input.MousePosition.X:F0}, {window.Input.MousePosition.Y:F0}";
        _sinceTitle = TimeSpan.Zero;
        _framesSinceTitle = 0;
    }
}
