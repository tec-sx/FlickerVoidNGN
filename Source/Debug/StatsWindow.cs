using Arch.Core;

using FlickerVoid.Engine.Graphics;

using Hexa.NET.ImGui;

namespace FlickerVoid.Engine.Debug;

/// <summary>The frame rate and frame time plot, the process's CPU use and memory, the GPU and its memory, and the entity count.</summary>
public sealed class StatsWindow : DebugWindow
{
    private readonly FrameStatistics _frames;
    private readonly ProcessStatistics _process;
    private readonly IGraphicsDevice _device;
    private readonly World _world;

    public StatsWindow(DebugUi ui, IGraphicsDevice device, World world)
        : base("Stats", "Engine")
    {
        _frames = ui.Frames;
        _process = ui.Process;
        _device = device;
        _world = world;
    }

    public override void Render()
    {
        ImGui.Text($"{_frames.FramesPerSecond:0.0} fps   {_frames.AverageMilliseconds:0.00} ms average   {_frames.MaxMilliseconds:0.00} ms slowest");
        float[] history = _frames.History.ToArray();
        if (history.Length > 0)
        {
            ImGui.PlotLines("##frames", ref history[0], history.Length, 0, "frame ms", 0, MathF.Max(_frames.MaxMilliseconds, 1) * 1.1f, new System.Numerics.Vector2(0, 60));
        }

        ImGui.SeparatorText("CPU");
        ImGui.Text($"Process {_process.CpuPercent:0.0}%% of {Environment.ProcessorCount} cores");

        ImGui.SeparatorText("Memory");
        ImGui.Text($"Working set {DebugFormat.Bytes(_process.WorkingSetBytes)}");
        ImGui.Text($"Managed heap {DebugFormat.Bytes(_process.ManagedBytes)}");
        ImGui.Text($"Collections gen 0/1/2: {_process.Collections[0]} / {_process.Collections[1]} / {_process.Collections[2]}");

        GraphicsInfo gpu = _device.GetInfo();
        ImGui.SeparatorText("GPU");
        ImGui.Text($"{gpu.AdapterName} (Vulkan {gpu.ApiVersion})");
        ImGui.Text($"Device memory {DebugFormat.Bytes(gpu.DeviceMemoryBytes)}");
        ImGui.Text($"Allocated by the engine {DebugFormat.Bytes(gpu.AllocatedBytes)} in {gpu.Allocations} allocations");

        ImGui.SeparatorText("World");
        ImGui.Text($"{_world.Size} entities");
    }
}
