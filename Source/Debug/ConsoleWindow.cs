using FlickerVoid.Engine.Runtime.Debugging;

using Hexa.NET.ImGui;

namespace FlickerVoid.Engine.Debug;

/// <summary>Runs typed lines on the game's debug console and shows what it answers; "help" lists the commands and variables.</summary>
public sealed class ConsoleWindow : DebugWindow
{
    private readonly ConsoleLog _log;
    private string _input = string.Empty;
    private bool _scrollToEnd;

    public ConsoleWindow(IDebugConsole console)
        : base("Console", "Engine")
    {
        _log = new ConsoleLog(console);
    }

    public override void Render()
    {
        float footer = ImGui.GetFrameHeightWithSpacing();
        if (ImGui.BeginChild("##lines", new System.Numerics.Vector2(0, -footer), ImGuiChildFlags.Borders))
        {
            foreach (string line in _log.Lines)
            {
                ImGui.TextUnformatted(line);
            }

            if (_scrollToEnd)
            {
                ImGui.SetScrollHereY(1);
                _scrollToEnd = false;
            }
        }

        ImGui.EndChild();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("##input", ref _input, 256, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            _log.Submit(_input);
            _input = string.Empty;
            _scrollToEnd = true;
            ImGui.SetKeyboardFocusHere(-1);
        }
    }
}
