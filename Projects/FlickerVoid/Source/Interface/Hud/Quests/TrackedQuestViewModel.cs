namespace FlickerVoid.Interface.Hud.Quests;

/// <summary>A quest the HUD tracks: its name and the objective to do next.</summary>
public sealed record TrackedQuestViewModel(string Name, string Objective);
