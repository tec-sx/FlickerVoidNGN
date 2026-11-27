using FlickerVoid.Engine.Graphics.Commands;

namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>
/// The CPU and GPU time of each render pass, averaged over a window of frames and published when the window is over, so the
/// numbers hold still long enough to read. GPU times arrive a few frames late, from the command list's last submission, and are
/// matched to passes by name.
/// </summary>
public sealed class RenderProfile
{
    /// <summary>About a second at 60 frames per second.</summary>
    public const int DefaultWindow = 60;

    private readonly int _window;
    private readonly List<Sample> _samples = [];
    private readonly Dictionary<string, int> _indices = [];
    private readonly List<PassTiming> _passes = [];
    private int _frames;

    public RenderProfile(int window = DefaultWindow)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(window, 1);
        _window = window;
    }

    /// <summary>The passes and GPU timers of the last window, in the order they first ran; empty before the first window is over.</summary>
    public IReadOnlyList<PassTiming> Passes => _passes;

    /// <summary>The average CPU time per frame of everything timed.</summary>
    public double CpuMilliseconds { get; private set; }

    /// <summary>The average GPU time per frame of the outermost timers.</summary>
    public double GpuMilliseconds { get; private set; }

    /// <summary>Adds CPU time to the named pass's this frame, as a pass is timed while it prepares and again while it records.</summary>
    public void AddCpu(string name, double milliseconds)
    {
        Sample sample = Find(name, 0);
        sample.FrameCpu += milliseconds;
        sample.CpuThisFrame = true;
    }

    /// <summary>Adds the GPU timings of a command list's last submission to this frame.</summary>
    public void AddGpu(IReadOnlyList<GpuTiming> timings)
    {
        foreach (GpuTiming timing in timings)
        {
            Sample sample = Find(timing.Name, timing.Depth);
            sample.FrameGpu += timing.Milliseconds;
            sample.GpuThisFrame = true;
        }
    }

    /// <summary>Ends the frame, and publishes the averages when it ends a window.</summary>
    public void EndFrame()
    {
        foreach (Sample sample in _samples)
        {
            if (sample.CpuThisFrame)
            {
                sample.CpuTotal += sample.FrameCpu;
                sample.CpuFrames++;
            }

            if (sample.GpuThisFrame)
            {
                sample.GpuTotal += sample.FrameGpu;
                sample.GpuFrames++;
            }

            sample.FrameCpu = 0;
            sample.FrameGpu = 0;
            sample.CpuThisFrame = false;
            sample.GpuThisFrame = false;
        }

        _frames++;
        if (_frames < _window)
        {
            return;
        }

        Publish();
        _frames = 0;
        _samples.Clear();
        _indices.Clear();
    }

    private void Publish()
    {
        _passes.Clear();
        double cpu = 0;
        double gpu = 0;
        foreach (Sample sample in _samples)
        {
            double cpuAverage = Average(sample.CpuTotal, sample.CpuFrames);
            double gpuAverage = Average(sample.GpuTotal, sample.GpuFrames);
            _passes.Add(new PassTiming(sample.Name, sample.Depth, cpuAverage, gpuAverage));
            cpu += cpuAverage;
            if (sample.Depth == 0)
            {
                gpu += gpuAverage;
            }
        }

        CpuMilliseconds = cpu;
        GpuMilliseconds = gpu;
    }

    private static double Average(double total, int frames)
    {
        if (frames == 0)
        {
            return 0;
        }

        return total / frames;
    }

    private Sample Find(string name, int depth)
    {
        if (_indices.TryGetValue(name, out int index))
        {
            return _samples[index];
        }

        Sample sample = new(name, depth);
        _indices.Add(name, _samples.Count);
        _samples.Add(sample);
        return sample;
    }

    /// <summary>What one pass or timer took over the window so far, and this frame.</summary>
    private sealed class Sample(string name, int depth)
    {
        public string Name { get; } = name;

        public int Depth { get; } = depth;

        public double FrameCpu { get; set; }

        public double FrameGpu { get; set; }

        public bool CpuThisFrame { get; set; }

        public bool GpuThisFrame { get; set; }

        public double CpuTotal { get; set; }

        public int CpuFrames { get; set; }

        public double GpuTotal { get; set; }

        public int GpuFrames { get; set; }
    }
}
