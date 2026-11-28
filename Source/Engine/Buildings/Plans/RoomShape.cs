using System.Numerics;

namespace FlickerVoid.Engine.Buildings.Plans;

/// <summary>A space the walls close: the index of its room record (-1 for none yet), its walls' centre lines running round to the left, and its floor's area inside them.</summary>
public sealed record RoomShape(int Record, IReadOnlyList<Vector2> Polygon, float Area);
