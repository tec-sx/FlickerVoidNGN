namespace FlickerVoid.Editor.Core.Buildings;

/// <summary>Something of a building picked in its creator: its kind, the level of its storey, its id, and a room's name.</summary>
public sealed record PlanPick(PlanPickKind Kind, int Level, int Id, string Room = "");
