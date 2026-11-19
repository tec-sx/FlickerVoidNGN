namespace FlickerVoid.Engine.Debug;

/// <summary>The windows listed under one main menu.</summary>
public sealed record DebugCategory(string Name, IReadOnlyList<DebugWindow> Windows);
