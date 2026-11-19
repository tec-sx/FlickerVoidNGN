using System.Runtime.InteropServices;

using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Devices;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.GlobalIllumination;

/// <summary>Finds the scene's first irradiance volume with probes, loads its probes once per file and uploads them when they change.</summary>
internal sealed class ProbeCache : IDisposable
{
    private readonly IGraphicsDevice _device;
    private readonly IContentManager _content;
    private readonly Dictionary<string, IrradianceProbes> _loaded = [];
    private IrradianceProbes? _uploaded;
    private IBuffer? _buffer;

    public ProbeCache(IGraphicsDevice device, IContentManager content)
    {
        _device = device;
        _content = content;
    }

    public ProbeSource? Find(RenderScene scene)
    {
        foreach (RenderProbeVolume volume in scene.ProbeVolumes)
        {
            if (volume.Volume.Probes is not null)
            {
                IrradianceProbes probes = Load(volume.Volume);
                return new ProbeSource(volume.Volume, volume.Center, probes);
            }
        }

        return null;
    }

    // The device releases the old buffer once the GPU is done with it.
    public uint Upload(IrradianceProbes probes)
    {
        if (!ReferenceEquals(probes, _uploaded))
        {
            _buffer?.Dispose();
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes<IrradianceProbe>(probes.Probes);
            BufferDescription description = new(bytes.Length, BufferUsage.ShaderRead) { Name = "Irradiance probes" };
            _buffer = _device.CreateBuffer(description, bytes);
            _uploaded = probes;
        }

        return _buffer!.BindlessIndex;
    }

    public void Dispose()
    {
        _buffer?.Dispose();
    }

    private IrradianceProbes Load(in IrradianceVolume volume)
    {
        string path = volume.Probes!;
        string fullPath = _content.GetFullPath(path);
        if (!_loaded.TryGetValue(fullPath, out IrradianceProbes? probes))
        {
            using FileStream stream = File.OpenRead(fullPath);
            probes = IrradianceProbes.Load(stream);
            _loaded.Add(fullPath, probes);
        }

        bool matches = probes.CountX == volume.ProbesX && probes.CountY == volume.ProbesY && probes.CountZ == volume.ProbesZ;
        if (!matches)
        {
            throw new InvalidDataException($"{path} holds {probes.CountX}x{probes.CountY}x{probes.CountZ} probes, but its volume has {volume.ProbesX}x{volume.ProbesY}x{volume.ProbesZ}; bake it again.");
        }

        return probes;
    }
}
