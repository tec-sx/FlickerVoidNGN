using System.Numerics;

namespace FlickerVoid.Engine.Rendering;

/// <summary>
/// A box round its entity's position (unrotated) filled with a grid of irradiance probes, which light the meshes inside it
/// with light bounced off the scene. The probes come from a file baked by <see cref="IrradianceBaker"/>.
/// </summary>
public struct IrradianceVolume
{
    public IrradianceVolume()
    {
    }

    /// <summary>The box's size in world units.</summary>
    public Vector3 Size { get; set; } = new(10, 4, 10);

    public int ProbesX { get; set; } = 4;

    public int ProbesY { get; set; } = 2;

    public int ProbesZ { get; set; } = 4;

    /// <summary>The baked probes file in the content folder; none lights the box by the ambient light alone.</summary>
    public string? Probes { get; set; }

    /// <summary>
    /// Where a probe sits: the box splits into equal cells, a probe in the middle of each, so probes keep off the walls and
    /// floors a volume is fitted to; surfaces between the outer probes and the box's sides take the outer probes' light.
    /// </summary>
    public readonly Vector3 GetProbePosition(Vector3 center, int x, int y, int z)
    {
        Vector3 share = new(GetShare(x, ProbesX), GetShare(y, ProbesY), GetShare(z, ProbesZ));
        Vector3 min = center - (Size / 2);
        return min + (Size * share);
    }

    private static float GetShare(int index, int count) => (index + 0.5f) / count;
}
