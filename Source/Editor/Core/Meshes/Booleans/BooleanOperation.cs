namespace FlickerVoid.Editor.Core.Meshes.Booleans;

/// <summary>How two solids combine: both together, the first without the second, or only where both are.</summary>
public enum BooleanOperation
{
    Add,
    Subtract,
    Intersect,
}
