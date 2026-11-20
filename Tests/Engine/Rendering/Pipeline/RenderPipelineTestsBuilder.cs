using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Tests.Pipeline;

/// <summary>Builds a render pipeline of substitute passes that record their calls into one shared log.</summary>
internal sealed class RenderPipelineTestsBuilder
{
    private readonly List<string> _calls = [];
    private readonly Dictionary<string, IRenderPass> _passes = [];

    public IReadOnlyList<string> Calls => _calls;

    public RenderContext Context { get; } = new(new RenderScene());

    public IRenderPass Pass(string name)
    {
        if (_passes.TryGetValue(name, out IRenderPass? existing))
        {
            return existing;
        }

        IRenderPass pass = Substitute.For<IRenderPass>();
        pass.When(candidate => candidate.Prepare(Arg.Any<RenderContext>())).Do(_ => _calls.Add($"{name}.Prepare"));
        pass.When(candidate => candidate.Execute(Arg.Any<RenderContext>())).Do(_ => _calls.Add($"{name}.Execute"));
        pass.When(candidate => candidate.Dispose()).Do(_ => _calls.Add($"{name}.Dispose"));
        _passes.Add(name, pass);
        return pass;
    }

    public RenderPipeline Build(params string[] names)
    {
        RenderPipeline pipeline = new();
        foreach (string name in names)
        {
            IRenderPass pass = Pass(name);
            pipeline.Add(pass);
        }

        return pipeline;
    }
}
