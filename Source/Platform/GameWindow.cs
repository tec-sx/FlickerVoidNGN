using System.Diagnostics;
using System.Numerics;

using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Runtime;
using FlickerVoid.Engine.Runtime.Debugging;

using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using Silk.NET.Input.Sdl;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;

namespace FlickerVoid.Engine.Platform;

/// <summary>The game's desktop window on SDL, feeding keyboard and mouse events to its input state.</summary>
public sealed class GameWindow : IGameWindow, IDisposable
{
    private readonly InputState _input = new();
    private readonly Game _game;
    private readonly IWindow _window;
    private readonly IInputContext _inputContext;

    /// <summary>Registers the SDL backends directly, so NativeAOT builds don't look for them by reflection, and lets Silk.NET find SDL on any Linux distribution.</summary>
    static GameWindow()
    {
        NativeLibraryPaths.AddRuntimesFolder();
        SdlWindowing.RegisterPlatform();
        SdlInput.RegisterPlatform();
    }

    /// <summary>Opens the window, registers it and its input as the game's services, and adds the "quit" console command.</summary>
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

        _input.EndFrame();
        game.Services.AddService<IGameWindow>(this);
        game.Services.AddService(Input);
        ConsoleCommand quit = new("quit", "Closes the window and ends the game.", Quit);
        game.DebugConsole.Add(quit);
    }

    public IInputState Input => _input;

    public string Title
    {
        get => _window.Title;
        set => _window.Title = value;
    }

    public Vector2D<int> Size => _window.FramebufferSize;

    public INativeWindow Native => _window.Native!;

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
            _input.EndFrame();

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

    private string Quit(string[] words)
    {
        Close();
        return "";
    }

    private void Attach(IKeyboard keyboard)
    {
        keyboard.KeyDown += HandleKeyDown;
        keyboard.KeyUp += HandleKeyUp;
        keyboard.KeyChar += HandleKeyChar;
    }

    private void Attach(IMouse mouse)
    {
        mouse.MouseDown += HandleMouseDown;
        mouse.MouseUp += HandleMouseUp;
        mouse.MouseMove += HandleMouseMove;
        mouse.Scroll += HandleScroll;
        _input.MoveMouse(mouse.Position);
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
            _input.ReleaseAll();
        }
    }

    private void HandleKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        _input.PressKey(key);
    }

    private void HandleKeyChar(IKeyboard keyboard, char character)
    {
        _input.TypeText(character);
    }

    private void HandleKeyUp(IKeyboard keyboard, Key key, int scancode)
    {
        _input.ReleaseKey(key);
    }

    private void HandleMouseDown(IMouse mouse, MouseButton button)
    {
        _input.PressButton(button);
    }

    private void HandleMouseUp(IMouse mouse, MouseButton button)
    {
        _input.ReleaseButton(button);
    }

    private void HandleMouseMove(IMouse mouse, Vector2 position)
    {
        _input.MoveMouse(position);
    }

    private void HandleScroll(IMouse mouse, ScrollWheel wheel)
    {
        _input.Scroll(wheel.Y);
    }
}
