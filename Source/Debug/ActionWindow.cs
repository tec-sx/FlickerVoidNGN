namespace FlickerVoid.Engine.Debug;

/// <summary>A debug panel whose contents are one method, for panels too small to deserve a class.</summary>
public sealed class ActionWindow : DebugWindow
{
    private readonly Action _render;

    public ActionWindow(string title, string category, Action render)
        : base(title, category)
    {
        _render = render;
    }

    public override void Render()
    {
        _render();
    }
}
