using Arch.Core;

using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Pipeline;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Engine.Debug;

/// <summary>Adds the engine's own debug windows to a <see cref="DebugUi"/>.</summary>
public static class EngineDebugWindows
{
    /// <summary>Adds Stats (frame rate, CPU, memory, GPU) and Console to the Engine menu.</summary>
    public static void AddEngineWindows(this DebugUi ui, Game game, IGraphicsDevice device)
    {
        StatsWindow stats = new(ui, device, game.World) { IsOpen = true };
        ConsoleWindow console = new(game.DebugConsole);
        ui.Windows.Add(stats);
        ui.Windows.Add(console);
    }

    /// <summary>Adds Rendering (features, through the game's <see cref="RenderingSettings"/> when given, and lighting mood) and Lights to the Rendering menu.</summary>
    public static void AddRenderingWindows(this DebugUi ui, SceneRenderer renderer, World world, RenderingSettings? settings = null)
    {
        RenderingWindow rendering = new(renderer, settings);
        LightsWindow lights = new(world);
        ui.Windows.Add(rendering);
        ui.Windows.Add(lights);
    }
}
