namespace FlickerVoid.Engine.Inventory.Inventories;

/// <summary>Uses left on a partly used unit: the stack's next one.</summary>
public sealed record ChargesLeft(int Remaining) : ItemInstanceData;
