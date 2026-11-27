using System.Diagnostics;

namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>
/// The ordered render passes that draw a frame; owns them and disposes them with itself. While the frame has a profile, it times
/// each pass on the CPU and wraps its GPU work in a timer named after it.
/// </summary>
public sealed class RenderPipeline : IDisposable
{
    private readonly List<IRenderPass> _passes = [];

    public IReadOnlyList<IRenderPass> Passes => _passes;

    public void Add(IRenderPass pass)
    {
        _passes.Add(pass);
    }

    public void InsertBefore(IRenderPass anchor, IRenderPass pass)
    {
        int index = IndexOf(anchor);
        _passes.Insert(index, pass);
    }

    public void InsertAfter(IRenderPass anchor, IRenderPass pass)
    {
        int index = IndexOf(anchor);
        _passes.Insert(index + 1, pass);
    }

    /// <summary>Puts the new pass in the old one's place and disposes the old one.</summary>
    public void Replace(IRenderPass old, IRenderPass pass)
    {
        int index = IndexOf(old);
        _passes[index] = pass;
        old.Dispose();
    }

    /// <summary>Takes the pass out without disposing it; the caller owns it again.</summary>
    public bool Remove(IRenderPass pass)
    {
        return _passes.Remove(pass);
    }

    /// <summary>The first pass of the given type, or null.</summary>
    public T? Find<T>()
        where T : class, IRenderPass
    {
        foreach (IRenderPass pass in _passes)
        {
            if (pass is T found)
            {
                return found;
            }
        }

        return null;
    }

    public void Prepare(RenderContext context)
    {
        RenderProfile? profile = context.Profile;
        foreach (IRenderPass pass in _passes)
        {
            long start = Stopwatch.GetTimestamp();
            pass.Prepare(context);
            if (profile is not null)
            {
                TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
                profile.AddCpu(pass.Name, elapsed.TotalMilliseconds);
            }
        }
    }

    public void Execute(RenderContext context)
    {
        RenderProfile? profile = context.Profile;
        foreach (IRenderPass pass in _passes)
        {
            if (profile is null)
            {
                pass.Execute(context);
                continue;
            }

            string name = pass.Name;
            long start = Stopwatch.GetTimestamp();
            context.Commands.BeginTimer(name);
            pass.Execute(context);
            context.Commands.EndTimer();
            TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
            profile.AddCpu(name, elapsed.TotalMilliseconds);
        }
    }

    public void Dispose()
    {
        foreach (IRenderPass pass in _passes)
        {
            pass.Dispose();
        }

        _passes.Clear();
    }

    private int IndexOf(IRenderPass pass)
    {
        int index = _passes.IndexOf(pass);
        if (index < 0)
        {
            throw new ArgumentException("The pass is not in the pipeline.", nameof(pass));
        }

        return index;
    }
}
