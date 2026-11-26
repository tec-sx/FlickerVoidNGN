namespace FlickerVoid.Editor.Core.Curves;

/// <summary>Where a free key's slope handles are drawn on the screen: the one it is arrived at and the one it is left at.</summary>
public readonly record struct CurveHandles(CurvePoint In, CurvePoint Out);
