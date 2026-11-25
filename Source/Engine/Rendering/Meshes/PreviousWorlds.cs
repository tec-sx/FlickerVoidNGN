using System.Numerics;

namespace FlickerVoid.Engine.Rendering.Meshes;

/// <summary>
/// Each mesh's world matrix in the frame before, by its render mesh's id, so passes can tell how far it moved; a mesh not drawn
/// in a frame is forgotten.
/// </summary>
public sealed class PreviousWorlds
{
    private Dictionary<int, Matrix4x4> _before = [];
    private Dictionary<int, Matrix4x4> _now = [];

    /// <summary>Starts a frame: what the last frame remembered becomes the frame before.</summary>
    public void Begin()
    {
        (_before, _now) = (_now, _before);
        _now.Clear();
    }

    /// <summary>Remembers the mesh's world this frame and returns the one it had in the frame before, or this one when it was not drawn then.</summary>
    public Matrix4x4 Remember(int mesh, in Matrix4x4 world)
    {
        _now[mesh] = world;
        if (_before.TryGetValue(mesh, out Matrix4x4 previous))
        {
            return previous;
        }

        return world;
    }
}
