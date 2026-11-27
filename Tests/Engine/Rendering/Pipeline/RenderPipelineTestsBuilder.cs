using FlickerVoid.Engine.Graphics.Commands;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Tests.Pipeline;

/// <summary>
/// Builds a render pipeline of substitute passes, named after themselves, that record their calls into one shared log, with
/// the GPU timers of a substitute command list and, while profiling, a render profile.
/// </summary>
internal sealed class RenderPipelineTestsBuilder
{
    private readonly List<string> _calls = [];
    private readonly Dictionary<string, IRenderPass> _passes = [];

    public IReadOnlyList<string> Calls => _calls;

    private int _window = RenderProfile.DefaultWindow;
    private bool _profiles;

    public RenderContext Context { get; } = new(new RenderScene());

    public RenderProfile Profile { get; private set; } = null!;

    public RenderPipelineTestsBuilder WithCommands()
    {
        ICommandList commands = Substitute.For<ICommandList>();
        commands.When(candidate => candidate.BeginTimer(Arg.Any<string>())).Do(call => _calls.Add($"Timer {call.Arg<string>()}"));
        commands.When(candidate => candidate.EndTimer()).Do(_ => _calls.Add("Timer end"));
        Context.Commands = commands;
        return this;
    }

    public RenderPipelineTestsBuilder WithProfile()
    {
        _profiles = true;
        return WithCommands();
    }

    public RenderPipelineTestsBuilder WithWindow(int frames)
    {
        _window = frames;
        return this;
    }

    public IRenderPass Pass(string name)
    {
        if (_passes.TryGetValue(name, out IRenderPass? existing))
        {
            return existing;
        }

        IRenderPass pass = Substitute.For<IRenderPass>();
        pass.Name.Returns(name);
        pass.When(candidate => candidate.Prepare(Arg.Any<RenderContext>())).Do(_ => _calls.Add($"{name}.Prepare"));
        pass.When(candidate => candidate.Execute(Arg.Any<RenderContext>())).Do(_ => _calls.Add($"{name}.Execute"));
        pass.When(candidate => candidate.Dispose()).Do(_ => _calls.Add($"{name}.Dispose"));
        _passes.Add(name, pass);
        return pass;
    }

    public RenderPipeline Build(params string[] names)
    {
        RenderPipeline pipeline = new();
        if (_profiles)
        {
            Profile = new RenderProfile(_window);
            Context.Profile = Profile;
        }

        foreach (string name in names)
        {
            IRenderPass pass = Pass(name);
            pipeline.Add(pass);
        }

        return pipeline;
    }
}
