using System.Numerics;

namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>
/// Where an opening stands, in the building's space: the foot of its middle on its wall's centre line, the way along the wall, the way
/// out of the wall's right side, and the wall's thickness, length and height above the floor.
/// </summary>
public readonly record struct OpeningFrame(Vector3 Origin, Vector3 Along, Vector3 Out, float WallThickness, float WallLength, float WallHeight);
