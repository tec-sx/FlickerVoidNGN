namespace FlickerVoid.Engine.Graphics.Pipelines;

/// <summary>Depth testing and writing of a pipeline.</summary>
public readonly record struct DepthState(bool Test, bool Write, CompareFunction Compare = CompareFunction.LessEqual)
{
    public static DepthState Disabled => new(false, false, CompareFunction.Always);
}

