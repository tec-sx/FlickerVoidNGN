using FlickerVoid.Engine.Graphics.Upscaling;
using FlickerVoid.Engine.Runtime.Debugging;

namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>
/// The renderer's feature switches as console variables (<c>r.RayTracing</c>, <c>r.Upscaling</c>), which settings files keep; the game
/// window applies them as they change, and the editor's viewport too, with an upscaling of its own.
/// </summary>
public sealed class RenderingSettings
{
    public RenderingSettings()
    {
        Variables = [LocalLights, Probes, RayTracing, LiveProbes, Upscaling, ViewportUpscaling, RayReconstruction, DepthFades, VSync, DebugView];
        LocalLights.Changed += HandleChanged;
        Probes.Changed += HandleChanged;
        RayTracing.Changed += HandleChanged;
        LiveProbes.Changed += HandleChanged;
        Upscaling.Changed += HandleChanged;
        ViewportUpscaling.Changed += HandleChanged;
        RayReconstruction.Changed += HandleChanged;
        DepthFades.Changed += HandleChanged;
        VSync.Changed += HandleChanged;
        DebugView.Changed += HandleChanged;
    }

    /// <summary>Raised when any of the variables is set.</summary>
    public event Action? Changed;

    public ConsoleVariable<bool> LocalLights { get; } = ConsoleVariable.Create("r.LocalLights", "Draws point and spot lights.", true);

    public ConsoleVariable<bool> Probes { get; } = ConsoleVariable.Create("r.Probes", "Lights the scene with its irradiance probes.", true);

    public ConsoleVariable<bool> RayTracing { get; } =
        ConsoleVariable.Create("r.RayTracing", "Traces shadows, ambient occlusion and live probes where the GPU has ray queries.", true);

    public ConsoleVariable<bool> LiveProbes { get; } = ConsoleVariable.Create("r.LiveProbes", "Updates the irradiance probes with traced rays every frame (with ray tracing).", true);

    public ConsoleVariable<UpscaleMode> Upscaling { get; } =
        ConsoleVariable.CreateEnum("r.Upscaling", "The game window's upscaling where the GPU has DLSS: Off, Quality (DLSS Quality) or Dlaa.", UpscaleMode.Quality);

    public ConsoleVariable<UpscaleMode> ViewportUpscaling { get; } =
        ConsoleVariable.CreateEnum("r.ViewportUpscaling", "The editor viewport's upscaling where the GPU has DLSS: Off, Quality or Dlaa; read when the editor starts.", UpscaleMode.Off);

    public ConsoleVariable<bool> RayReconstruction { get; } =
        ConsoleVariable.Create("r.RayReconstruction", "Denoises traced shadows and occlusion with DLSS Ray Reconstruction while DLSS upscales.", true);

    public ConsoleVariable<bool> DepthFades { get; } = ConsoleVariable.Create("r.DepthFades", "Draws the main camera's depth fade.", true);

    public ConsoleVariable<bool> VSync { get; } =
        ConsoleVariable.Create("r.VSync", "Waits for the display's refresh before showing each frame of the game window, capping the frame rate at it.", false);

    public ConsoleVariable<DebugView> DebugView { get; } =
        ConsoleVariable.CreateEnum("r.DebugView", "Shows a G-buffer target in place of the lit frame.", Pipeline.DebugView.Lit, saved: false);

    public IReadOnlyList<ConsoleVariable> Variables { get; }

    /// <summary>Settings holding a renderer's switches as they are, for a renderer no game sets up.</summary>
    public static RenderingSettings Capture(SceneRenderer renderer)
    {
        RenderingSettings settings = new();
        settings.LocalLights.Value = renderer.LocalLights;
        settings.Probes.Value = renderer.Probes;
        settings.RayTracing.Value = renderer.RayTracing;
        settings.LiveProbes.Value = renderer.LiveProbes;
        settings.Upscaling.Value = renderer.Upscaling;
        settings.RayReconstruction.Value = renderer.RayReconstruction;
        settings.DepthFades.Value = renderer.DepthFades;
        settings.DebugView.Value = renderer.DebugView;
        return settings;
    }

    public void Register(IDebugConsole console)
    {
        foreach (ConsoleVariable variable in Variables)
        {
            console.Add(variable);
        }
    }

    /// <summary>Sets a game window's renderer as the variables say.</summary>
    public void Apply(SceneRenderer renderer)
    {
        renderer.LocalLights = LocalLights.Value;
        renderer.Probes = Probes.Value;
        renderer.RayTracing = RayTracing.Value;
        renderer.LiveProbes = LiveProbes.Value;
        renderer.Upscaling = Upscaling.Value;
        renderer.RayReconstruction = RayReconstruction.Value;
        renderer.DepthFades = DepthFades.Value;
        renderer.DebugView = DebugView.Value;
    }

    /// <summary>Sets the editor viewport's renderer as the variables say, with the viewport's upscaling.</summary>
    public void ApplyToViewport(SceneRenderer renderer)
    {
        Apply(renderer);
        renderer.Upscaling = ViewportUpscaling.Value;
    }

    private void HandleChanged<T>(T value)
    {
        Changed?.Invoke();
    }
}
