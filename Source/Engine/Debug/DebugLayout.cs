namespace FlickerVoid.Engine.Debug;

/// <summary>Which windows were open, kept between runs.</summary>
public sealed class DebugLayout
{
    public List<string> OpenWindows { get; set; } = [];
}
