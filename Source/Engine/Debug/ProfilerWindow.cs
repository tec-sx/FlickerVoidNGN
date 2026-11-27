using System.Numerics;

using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Rendering.Pipeline;

using Hexa.NET.ImGui;

namespace FlickerVoid.Engine.Debug;

/// <summary>
/// Shows where a frame's time goes: the scene renderer's passes as a timeline of boxes on the CPU and on the GPU against the
/// 60 frames per second budget, and a table of each pass's time, averaged over about a second. Turns the renderer's profiling
/// on once it is first shown.
/// </summary>
public sealed class ProfilerWindow : DebugWindow
{
    /// <summary>The budget of a frame at 60 frames per second; the timeline is at least this wide.</summary>
    private const double BudgetMilliseconds = 1000.0 / 60;

    /// <summary>How much wider than the budget the timeline is at least, so the budget's line stands inside it.</summary>
    private const double BudgetShare = 1.25;

    private const float BarHeight = 18;
    private const float LabelWidth = 36;

    private static readonly Vector4[] Colors =
    [
        new(0.90f, 0.55f, 0.25f, 1),
        new(0.35f, 0.65f, 0.90f, 1),
        new(0.55f, 0.80f, 0.35f, 1),
        new(0.85f, 0.40f, 0.55f, 1),
        new(0.95f, 0.80f, 0.35f, 1),
        new(0.55f, 0.50f, 0.90f, 1),
        new(0.40f, 0.80f, 0.75f, 1),
        new(0.80f, 0.60f, 0.45f, 1),
    ];

    private readonly SceneRenderer _renderer;

    public ProfilerWindow(SceneRenderer renderer)
        : base("Profiler", "Rendering")
    {
        _renderer = renderer;
    }

    public override Vector2 DefaultSize => new(560, 440);

    public override void Render()
    {
        _renderer.Profiling = true;
        RenderProfile profile = _renderer.Profile;
        IReadOnlyList<PassTiming> passes = profile.Passes;
        if (passes.Count == 0)
        {
            ImGui.TextDisabled("Measuring the first second of frames...");
            return;
        }

        bool timestamps = _renderer.Device.Features.HasFlag(GraphicsFeatures.Timestamps);
        if (timestamps)
        {
            ImGui.Text($"CPU {profile.CpuMilliseconds:0.00} ms   GPU {profile.GpuMilliseconds:0.00} ms   per frame, averaged over a second");
        }
        else
        {
            ImGui.Text($"CPU {profile.CpuMilliseconds:0.00} ms per frame, averaged over a second; this GPU writes no timestamps");
        }

        double slowest = Math.Max(profile.CpuMilliseconds, profile.GpuMilliseconds);
        double scale = Math.Max(BudgetMilliseconds * BudgetShare, slowest * 1.05);
        RenderTimeline("CPU", passes, gpu: false, scale);
        if (timestamps)
        {
            RenderTimeline("GPU", passes, gpu: true, scale);
        }

        ImGui.TextDisabled($"The line marks {BudgetMilliseconds:0.0} ms, a frame at 60 frames per second.");
        RenderTable(passes, timestamps);
    }

    /// <summary>Draws the outermost passes as boxes side by side, as wide as their share of the scale, with their name on hover.</summary>
    private static void RenderTimeline(string label, IReadOnlyList<PassTiming> passes, bool gpu, double scale)
    {
        ImGui.Text(label);
        ImGui.SameLine(LabelWidth);
        Vector2 origin = ImGui.GetCursorScreenPos();
        Vector2 available = ImGui.GetContentRegionAvail();
        float width = MathF.Max(available.X, 50);
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        Vector2 backgroundEnd = origin + new Vector2(width, BarHeight);
        uint background = ImGui.GetColorU32(ImGuiCol.FrameBg);
        drawList.AddRectFilled(origin, backgroundEnd, background);

        float x = origin.X;
        for (int index = 0; index < passes.Count; index++)
        {
            PassTiming pass = passes[index];
            if (pass.Depth > 0)
            {
                continue;
            }

            double milliseconds = pass.CpuMilliseconds;
            if (gpu)
            {
                milliseconds = pass.GpuMilliseconds;
            }

            float boxWidth = (float)(milliseconds / scale * width);
            Vector2 start = new(x, origin.Y);
            Vector2 end = new(x + boxWidth, origin.Y + BarHeight);
            Vector4 color = Colors[index % Colors.Length];
            uint fill = ImGui.GetColorU32(color);
            drawList.AddRectFilled(start, end, fill);
            if (boxWidth > 0 && ImGui.IsMouseHoveringRect(start, end))
            {
                ImGui.SetTooltip($"{pass.Name}: {milliseconds:0.000} ms");
            }

            x += boxWidth;
        }

        float budgetX = origin.X + (float)(BudgetMilliseconds / scale * width);
        Vector2 lineStart = new(budgetX, origin.Y - 2);
        Vector2 lineEnd = new(budgetX, origin.Y + BarHeight + 2);
        uint lineColor = ImGui.GetColorU32(ImGuiCol.Text);
        drawList.AddLine(lineStart, lineEnd, lineColor);
        Vector2 size = new(width, BarHeight);
        ImGui.Dummy(size);
    }

    private static void RenderTable(IReadOnlyList<PassTiming> passes, bool timestamps)
    {
        int columns = 2;
        if (timestamps)
        {
            columns = 3;
        }

        ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp;
        if (!ImGui.BeginTable("passes", columns, flags))
        {
            return;
        }

        ImGui.TableSetupColumn("Pass");
        ImGui.TableSetupColumn("CPU ms");
        if (timestamps)
        {
            ImGui.TableSetupColumn("GPU ms");
        }

        ImGui.TableHeadersRow();
        for (int index = 0; index < passes.Count; index++)
        {
            PassTiming pass = passes[index];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if (pass.Depth == 0)
            {
                Vector4 color = Colors[index % Colors.Length];
                ImGui.TextColored(color, pass.Name);
            }
            else
            {
                string indent = new(' ', pass.Depth * 2);
                ImGui.Text(indent + pass.Name);
            }

            ImGui.TableNextColumn();
            if (pass.Depth == 0)
            {
                ImGui.Text($"{pass.CpuMilliseconds:0.000}");
            }

            if (timestamps)
            {
                ImGui.TableNextColumn();
                ImGui.Text($"{pass.GpuMilliseconds:0.000}");
            }
        }

        ImGui.EndTable();
    }
}
