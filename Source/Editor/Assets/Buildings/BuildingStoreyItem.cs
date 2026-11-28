namespace FlickerVoid.Editor.Assets.Buildings;

/// <summary>A storey in the storey switcher: its level and the name it shows (Cellar, Ground, 1, 2).</summary>
public sealed record BuildingStoreyItem(int Level, string Name);
