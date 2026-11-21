using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Graphics.Pipelines;
using FlickerVoid.Engine.Graphics.Textures;
using FlickerVoid.Engine.Hosting;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Platform.Windowing;

using Hexa.NET.ImGui;

using Silk.NET.Input;

namespace FlickerVoid.Engine.Debug;

/// <summary>
/// The development-only debug interface, drawn with Dear ImGui over the game: a main menu bar with a menu for each window
/// category, and the windows opened from it. Engine and game code add panels to <see cref="Windows"/>. F1 shows and hides it.
/// Call <see cref="Update"/> each frame, then <see cref="Draw"/> after the scene is drawn into the target. Only one can exist at a time.
/// </summary>
public sealed unsafe class DebugUi : IFrameOverlay, IPointerCapture
{
    private const string LayoutFile = "debug-ui.json";
    private const string ImGuiFile = "debug-ui.ini";
    private const float ProcessSampleSeconds = 0.5f;

    private readonly IGameWindow _window;
    private readonly DebugUiRenderer _renderer;
    private readonly ImGuiContextPtr _context;
    private readonly string? _settingsDirectory;
    private readonly nint _iniPath;
    private ImDrawDataPtr _drawData;
    private float _sinceProcessSample;
    private bool _disposed;

    /// <summary>Creates the UI for a window and a target of the given format; with a settings directory it keeps the layout there between runs.</summary>
    public DebugUi(IGraphicsDevice device, IGameWindow window, PixelFormat targetFormat, string? settingsDirectory = null)
    {
        _window = window;
        _settingsDirectory = settingsDirectory;
        ShaderLibrary shaders = ShaderLibrary.ForEngine("Debug");
        _renderer = new DebugUiRenderer(device, shaders, targetFormat);
        _context = ImGui.CreateContext();
        ImGui.SetCurrentContext(_context);
        ImGuiIOPtr io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset | ImGuiBackendFlags.RendererHasTextures;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        if (settingsDirectory is not null)
        {
            Directory.CreateDirectory(settingsDirectory);
            string iniPath = Path.Combine(settingsDirectory, ImGuiFile);
            _iniPath = Marshal.StringToCoTaskMemUTF8(iniPath);
            io.IniFilename = (byte*)_iniPath;
        }
        else
        {
            io.IniFilename = null;
        }

        ImGui.StyleColorsDark();
        Process = new ProcessStatistics(new RuntimeProcessProbe());
    }

    public DebugWindows Windows { get; } = new();

    public FrameStatistics Frames { get; } = new();

    public ProcessStatistics Process { get; }

    /// <summary>Whether the UI is shown; F1 toggles it.</summary>
    public bool Visible { get; set; }

    /// <summary>True while the pointer is over a debug window or menu or one is dragged, so the game should ignore the mouse.</summary>
    public bool WantsMouse { get; private set; }

    /// <summary>The same as <see cref="WantsMouse"/>, so the game's input actions leave the mouse's buttons to the debug interface.</summary>
    public bool CapturesPointer => WantsMouse;

    /// <summary>True while a text box has focus, so the game should ignore the keyboard.</summary>
    public bool WantsKeyboard { get; private set; }

    /// <summary>Opens the windows the layout file lists; call it after adding the windows.</summary>
    public void LoadLayout()
    {
        string? path = GetLayoutPath();
        if (path is not null && File.Exists(path))
        {
            string json = File.ReadAllText(path);
            Windows.LoadLayout(json);
        }
    }

    public void SaveLayout()
    {
        string? path = GetLayoutPath();
        if (path is not null)
        {
            string json = Windows.SaveLayout();
            File.WriteAllText(path, json);
        }
    }

    /// <summary>Builds the frame: handles input, then draws the menu bar and the open windows into draw data.</summary>
    public void Update(float deltaSeconds)
    {
        ImGui.SetCurrentContext(_context);
        Frames.Add(deltaSeconds);
        _sinceProcessSample += deltaSeconds;
        if (_sinceProcessSample >= ProcessSampleSeconds)
        {
            Process.Update();
            _sinceProcessSample = 0;
        }

        if (_window.Input.WasPressed(Key.F1))
        {
            Visible = !Visible;
        }

        if (!Visible)
        {
            _drawData = default;
            WantsMouse = false;
            WantsKeyboard = false;
            return;
        }

        ImGuiIOPtr io = ImGui.GetIO();
        io.DisplaySize = new System.Numerics.Vector2(_window.Size.X, _window.Size.Y);
        io.DeltaTime = MathF.Max(deltaSeconds, 1e-5f);
        ImGuiInput.Feed(io, _window.Input);
        ImGui.NewFrame();
        DrawMenuBar();
        DrawWindows();
        ImGui.Render();
        _drawData = ImGui.GetDrawData();
        WantsMouse = io.WantCaptureMouse;
        WantsKeyboard = io.WantCaptureKeyboard;
    }

    /// <summary>Draws the frame built by <see cref="Update"/> over the target, which keeps what it shows; does nothing while hidden.</summary>
    public void Draw(ICommandList commands, ITexture target)
    {
        if (!Visible || !_drawData.Valid)
        {
            return;
        }

        _renderer.Draw(commands, target, _drawData);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SaveLayout();
        ImGui.SetCurrentContext(_context);
        _renderer.Dispose();
        ImGui.DestroyContext(_context);
        if (_iniPath != 0)
        {
            Marshal.FreeCoTaskMem(_iniPath);
        }
    }

    private string? GetLayoutPath()
    {
        if (_settingsDirectory is null)
        {
            return null;
        }

        return Path.Combine(_settingsDirectory, LayoutFile);
    }

    private void DrawMenuBar()
    {
        if (!ImGui.BeginMainMenuBar())
        {
            return;
        }

        foreach (DebugCategory category in Windows.Categories)
        {
            if (!ImGui.BeginMenu(category.Name))
            {
                continue;
            }

            foreach (DebugWindow window in category.Windows)
            {
                bool open = window.IsOpen;
                if (ImGui.MenuItem(window.Title, string.Empty, ref open, true))
                {
                    window.IsOpen = open;
                }
            }

            ImGui.EndMenu();
        }

        string summary = $"{Frames.FramesPerSecond:0} fps  {Frames.AverageMilliseconds:0.0} ms";
        float width = ImGui.CalcTextSize(summary).X + 16;
        ImGui.SameLine(ImGui.GetWindowWidth() - width);
        ImGui.TextUnformatted(summary);
        ImGui.EndMainMenuBar();
    }

    private void DrawWindows()
    {
        foreach (DebugWindow window in Windows.All)
        {
            if (!window.IsOpen)
            {
                continue;
            }

            bool open = true;
            if (ImGui.Begin(window.Title, ref open))
            {
                window.Render();
            }

            ImGui.End();
            window.IsOpen = open;
        }
    }
}
