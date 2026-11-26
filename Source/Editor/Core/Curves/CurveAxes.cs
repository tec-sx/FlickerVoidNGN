namespace FlickerVoid.Editor.Core.Curves;

/// <summary>Which axes of the curve view a zoom changes.</summary>
[Flags]
public enum CurveAxes
{
    Time = 1,
    Value = 2,
    Both = Time | Value,
}
