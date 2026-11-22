using System.Numerics;

namespace FlickerVoid.Engine.Characters.Movement;

/// <summary>Finds a way around obstacles for characters to walk; the movement system walks straight without one.</summary>
public interface IPathFinder
{
    /// <summary>
    /// Fills <paramref name="corners"/> with the points to walk through from <paramref name="from"/>, ending at <paramref name="to"/> or
    /// as near to it as can be reached; false when there is no way from where the character stands.
    /// </summary>
    bool TryFindPath(Vector3 from, Vector3 to, List<Vector3> corners);
}
