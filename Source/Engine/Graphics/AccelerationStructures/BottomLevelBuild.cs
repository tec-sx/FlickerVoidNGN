namespace FlickerVoid.Engine.Graphics.AccelerationStructures;

/// <summary>A bottom-level acceleration structure to build from its geometry, or to refit to the geometry's new vertex positions.</summary>
public readonly record struct BottomLevelBuild(IAccelerationStructure Structure, BottomLevelGeometry Geometry, bool Refit = false);
