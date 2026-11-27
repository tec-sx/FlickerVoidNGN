using FlickerVoid.Editor.Core.Buildings;

namespace FlickerVoid.Editor.Assets.Buildings;

/// <summary>
/// One line of the building's parts tree: its name, its icon's name, how deep it sits, the part it shows (when one was made) and what
/// picking it picks in the plan.
/// </summary>
public sealed record BuildingNode(string Name, string Icon, int Depth, string? PartId, PlanPick? Pick);
