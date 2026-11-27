namespace FlickerVoid.Engine.Rendering.Instancing;

/// <summary>
/// Copies of one model drawn together, such as a chunk of grass or the bushes along a street, held by an
/// <see cref="InstancedMesh"/> on the chunk's entity. Whatever owns the chunk writes its copies and the renderer only reads them:
/// <see cref="Rewrite"/> replaces them, or <see cref="Changed"/> follows changes made in place, so the next frame draws them.
/// </summary>
public sealed class InstanceBatch
{
    private Instance[] _copies = [];

    public InstanceBatch(string mesh)
    {
        Mesh = mesh;
    }

    /// <summary>The model every copy draws, as a <see cref="Meshes.MeshRenderer"/> names it.</summary>
    public string Mesh { get; set; }

    /// <summary>A <c>.fvmat</c> file for each of the model's parts, as <see cref="Meshes.MeshRenderer.Materials"/>.</summary>
    public string[]? Materials { get; set; }

    /// <summary>The size of a copy whose <see cref="Instance.Scale"/> is 0, as a share of the model's.</summary>
    public float MinScale { get; set; } = 1;

    /// <summary>The size of a copy whose <see cref="Instance.Scale"/> is 1.</summary>
    public float MaxScale { get; set; } = 1;

    /// <summary>A simpler model the whole batch draws in place of <see cref="Mesh"/> beyond <see cref="StandInDistance"/>, if any.</summary>
    public string? StandIn { get; set; }

    /// <summary>How far from the focus (else the camera) the batch's box may be before it draws its <see cref="StandIn"/>.</summary>
    public float StandInDistance { get; set; } = float.PositiveInfinity;

    public int Count { get; private set; }

    /// <summary>The copies, to read or change in place; call <see cref="Changed"/> after changing them.</summary>
    public Span<Instance> Copies => _copies.AsSpan(0, Count);

    /// <summary>Goes up each time the copies change, so the renderer knows to send them to the GPU again.</summary>
    public uint Version { get; private set; }

    /// <summary>Makes room for that many copies, keeping as many of the old as fit, and gives them to be written.</summary>
    public Span<Instance> Rewrite(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > _copies.Length)
        {
            int capacity = Math.Max(count, _copies.Length * 2);
            Array.Resize(ref _copies, capacity);
        }

        Count = count;
        Changed();
        return Copies;
    }

    /// <summary>The scale range as the shaders read it: the smallest and the largest as two halves.</summary>
    internal uint PackScales()
    {
        ushort smallest = BitConverter.HalfToUInt16Bits((Half)MinScale);
        ushort largest = BitConverter.HalfToUInt16Bits((Half)MaxScale);
        return smallest | (uint)largest << 16;
    }

    /// <summary>Says the copies changed in place.</summary>
    public void Changed()
    {
        Version++;
    }
}
