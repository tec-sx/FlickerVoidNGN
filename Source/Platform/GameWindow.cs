using System.Diagnostics;
using System.Numerics;

using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Runtime;

using Silk.NET.Input;
using Silk.NET.Input.Sdl;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;

namespace FlickerVoid.Engine.Platform;

/// <summary>The game's desktop window on SDL: runs the game loop and feeds keyboard and mouse input to <see cref="Input"/>.</summary>
public sealed class GameWindow : IDisposable
{
    private readonly Game _game;
    private readonly IWindow _window;
    private readonly IInputContext _inputContext;

    /// <summary>Registers the SDL backends directly, so NativeAOT builds don't look for them by reflection.</summary>
    static GameWindow()
    {
        SdlWindowing.RegisterPlatform();
        SdlInput.RegisterPlatform();
    }

    /// <summary>Opens the window and registers it and its <see cref="InputState"/> as the game's services.</summary>
    public GameWindow(Game game, string title, int width, int height)
    {
        _game = game;
        Vector2D<int> size = new(width, height);
        WindowOptions options = WindowOptions.Default with { Title = title, Size = size, API = GraphicsAPI.None };
        _window = Window.Create(options);
        _window.Initialize();
        _window.FocusChanged += HandleFocusChanged;

        _inputContext = _window.CreateInput();
        _inputContext.ConnectionChanged += HandleConnectionChanged;
        foreach (IKeyboard keyboard in _inputContext.Keyboards)
        {
            Attach(keyboard);
        }

        foreach (IMouse mouse in _inputContext.Mice)
        {
            Attach(mouse);
        }

        Input.EndFrame();
        game.Services.AddService(this);
        game.Services.AddService(Input);
    }

    public InputState Input { get; } = new();

    public string Title
    {
        get => _window.Title;
        set => _window.Title = value;
    }

    /// <summary>Runs until the window closes: each frame handles the window's events, ticks the game with the real time since the last frame, then ends the input frame.</summary>
    public void Run()
    {
        Stopwatch clock = Stopwatch.StartNew();
        TimeSpan last = TimeSpan.Zero;
        while (true)
        {
            _window.DoEvents();
            if (_window.IsClosing)
            {
                break;
            }

            TimeSpan now = clock.Elapsed;
            _game.Tick(now - last);
            last = now;
            Input.EndFrame();

            // Nothing presents frames with vsync until the renderer arrives, so give the core back between frames.
            Thread.Sleep(1);
        }
    }

    public void Close()
    {
        _window.Close();
    }

    public void Dispose()
    {
        _inputContext.Dispose();
        _window.Reset();
        _window.Dispose();
    }

    private void Attach(IKeyboard keyboard)
    {
        keyboard.KeyDown += HandleKeyDown;
        keyboard.KeyUp += HandleKeyUp;
    }

    private void Attach(IMouse mouse)
    {
        mouse.MouseDown += HandleMouseDown;
        mouse.MouseUp += HandleMouseUp;
        mouse.MouseMove += HandleMouseMove;
        mouse.Scroll += HandleScroll;
        Input.MoveMouse(mouse.Position);
    }

    private void HandleConnectionChanged(IInputDevice device, bool connected)
    {
        if (!connected)
        {
            return;
        }

        if (device is IKeyboard keyboard)
        {
            Attach(keyboard);
        }

        if (device is IMouse mouse)
        {
            Attach(mouse);
        }
    }

    private void HandleFocusChanged(bool focused)
    {
        if (!focused)
        {
            Input.ReleaseAll();
        }
    }

    private void HandleKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        Input.PressKey(key);
    }

    private void HandleKeyUp(IKeyboard keyboard, Key key, int scancode)
    {
        Input.ReleaseKey(key);
    }

    private void HandleMouseDown(IMouse mouse, MouseButton button)
    {
        Input.PressButton(button);
    }

    private void HandleMouseUp(IMouse mouse, MouseButton button)
    {
        Input.ReleaseButton(button);
    }

    private void HandleMouseMove(IMouse mouse, Vector2 position)
    {
        Input.MoveMouse(position);
    }

    private void HandleScroll(IMouse mouse, ScrollWheel wheel)
    {
        Input.Scroll(wheel.Y);
    }
}
