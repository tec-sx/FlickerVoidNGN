namespace FlickerVoid.Editor.Viewports;

/// <summary>A run of tools in the <see cref="ToolRailViewModel"/>; every group after the first has a hairline above it.</summary>
public sealed record ToolGroup(IReadOnlyList<ToolItem> Items, bool HasLine);
