using System.Numerics;
using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Rendering.Meshes;
using FlickerVoid.Engine.Runtime.Geometry;

namespace FlickerVoid.Engine.Rendering.Instancing;

/// <summary>
/// One batch's buffers on the GPU: its copies in an upload buffer for each frame in flight, rewritten whenever that frame's copy
/// is older than the batch's version, so a brush can rewrite a chunk every frame without touching frames the GPU still draws;
/// the list of copies the view's cull keeps; and the model's parts' indirect arguments, made again when the model changes. Keeps
/// the box round the copies, worked out again when they change.
/// </summary>
internal sealed class BatchBuffers : IDisposable
{
    private const int CountSize = 4;

    private const int ArgumentsSize = 20;

    private readonly IGraphicsDevice _device;
    private readonly IBuffer?[] _copies;
    private readonly uint[] _versions;
    private readonly bool[] _written;
    private GpuModel? _argumentsModel;
    private uint _boundsVersion;
    private bool _hasBounds;

    public BatchBuffers(IGraphicsDevice device, int framesInFlight)
    {
        _device = device;
        _copies = new IBuffer?[framesInFlight];
        _versions = new uint[framesInFlight];
        _written = new bool[framesInFlight];
    }

    public IBuffer? Visible { get; private set; }

    public IBuffer? Arguments { get; private set; }

    public Bounds Bounds { get; private set; }

    public static long ArgumentsOffset(int part)
    {
        return CountSize + ((long)part * ArgumentsSize);
    }

    /// <summary>The box round every copy's sphere, for the model's sphere; worked out again when the copies change.</summary>
    public Bounds FindBounds(InstanceBatch batch, ModelSphere sphere)
    {
        if (_hasBounds && _boundsVersion == batch.Version)
        {
            return Bounds;
        }

        Bounds bounds = Bounds.Empty;
        foreach (Instance copy in batch.Copies)
        {
            Matrix4x4 world = copy.ToMatrix(batch.MinScale, batch.MaxScale);
            Vector3 center = Vector3.Transform(sphere.Center, world);
            float size = float.Lerp(batch.MinScale, batch.MaxScale, copy.Scale);
            Vector3 reach = new(sphere.Radius * size);
            Bounds around = new(center - reach, center + reach);
            bounds = bounds.Include(around);
        }

        Bounds = bounds;
        _boundsVersion = batch.Version;
        _hasBounds = true;
        return bounds;
    }

    /// <summary>The frame's buffer of copies, holding the batch's latest, and room for the cull and the model's arguments.</summary>
    public IBuffer Update(InstanceBatch batch, GpuModel model, int slot)
    {
        IBuffer copies = EnsureCopies(batch, slot);
        EnsureVisible(batch.Count);
        EnsureArguments(model);
        return copies;
    }

    public void Dispose()
    {
        foreach (IBuffer? copies in _copies)
        {
            copies?.Dispose();
        }

        Visible?.Dispose();
        Arguments?.Dispose();
    }

    private IBuffer EnsureCopies(InstanceBatch batch, int slot)
    {
        long size = Math.Max(batch.Count, 1) * (long)Marshal.SizeOf<Instance>();
        IBuffer? copies = _copies[slot];
        if (copies is null || copies.Description.Size < size)
        {
            copies?.Dispose();
            long capacity = (long)BitOperations.RoundUpToPowerOf2((ulong)size);
            BufferDescription description = new(capacity, BufferUsage.ShaderRead, MemoryLocation.Upload) { Name = $"Instance batch copies {slot}" };
            copies = _device.CreateBuffer(description);
            _copies[slot] = copies;
            _written[slot] = false;
        }

        if (!_written[slot] || _versions[slot] != batch.Version)
        {
            ReadOnlySpan<Instance> written = batch.Copies;
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(written);
            copies.Write(bytes);
            _versions[slot] = batch.Version;
            _written[slot] = true;
        }

        return copies;
    }

    private void EnsureVisible(int count)
    {
        long size = Math.Max(count, 1) * 4L;
        if (Visible is not null && Visible.Description.Size >= size)
        {
            return;
        }

        Visible?.Dispose();
        long capacity = (long)BitOperations.RoundUpToPowerOf2((ulong)size);
        BufferDescription description = new(capacity, BufferUsage.ShaderRead | BufferUsage.ShaderWrite) { Name = "Instance batch visible copies" };
        Visible = _device.CreateBuffer(description);
    }

    /// <summary>The model's parts' arguments, each drawing all its indices and no copies until the cull counts them.</summary>
    private void EnsureArguments(GpuModel model)
    {
        if (ReferenceEquals(model, _argumentsModel) && Arguments is not null)
        {
            return;
        }

        int parts = model.Parts.Count;
        uint[] words = new uint[1 + (parts * 5)];
        for (int part = 0; part < parts; part++)
        {
            words[1 + (part * 5)] = (uint)model.Parts[part].IndexCount;
        }

        Arguments?.Dispose();
        byte[] data = MemoryMarshal.AsBytes(words.AsSpan()).ToArray();
        BufferDescription description = new(data.Length, BufferUsage.ShaderWrite | BufferUsage.Indirect) { Name = "Instance batch arguments" };
        Arguments = _device.CreateBuffer(description, data);
        _argumentsModel = model;
    }
}
