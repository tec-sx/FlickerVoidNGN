namespace FlickerVoid.Editor.Core.Creators;

/// <summary>What a dragged mesh tool takes besides its amount: a bevel's rows and roundness.</summary>
public readonly record struct MeshDragOptions(int Rows = 1, bool Round = false);
