using Arch.System;

using Silk.NET.Input;

namespace FlickerVoidGame.Launcher;

/// <summary>Closes the window on Escape, or after a number of frames when the limit is above zero.</summary>
public sealed class CloseSystem(IGameWindow window, int frameLimit) : ISystem<GameTime>
{
    private int _frames;

    public void Initialize()
    {
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        _frames++;
        bool limitReached = frameLimit > 0 && _frames >= frameLimit;
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
}
