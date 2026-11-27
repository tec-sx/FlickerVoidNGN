using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Tests.Pipeline;

public sealed class RenderPipelineTests
{
    private readonly RenderPipelineTestsBuilder _builder = new();

    [Fact]
    public void PreparesEveryPassInOrder()
    {
        RenderPipeline pipeline = _builder.Build("Shadows", "Opaque");

        pipeline.Prepare(_builder.Context);

        _builder.Calls.Should().Equal("Shadows.Prepare", "Opaque.Prepare");
    }

    [Fact]
    public void ExecutesEveryPassInOrder()
    {
        RenderPipeline pipeline = _builder.Build("Shadows", "Opaque", "Post");

        pipeline.Execute(_builder.Context);

        _builder.Calls.Should().Equal("Shadows.Execute", "Opaque.Execute", "Post.Execute");
    }

    [Fact]
    public void InsertsAPassBeforeAnother()
    {
        RenderPipeline pipeline = _builder.Build("Opaque", "Post");
        IRenderPass fog = _builder.Pass("Fog");
        IRenderPass post = _builder.Pass("Post");

        pipeline.InsertBefore(post, fog);
        pipeline.Execute(_builder.Context);

        _builder.Calls.Should().Equal("Opaque.Execute", "Fog.Execute", "Post.Execute");
    }

    [Fact]
    public void ReplacesAPassAndDisposesTheOldOne()
    {
        RenderPipeline pipeline = _builder.Build("Opaque", "Post");
        IRenderPass post = _builder.Pass("Post");
        IRenderPass styled = _builder.Pass("Styled");

        pipeline.Replace(post, styled);
        pipeline.Execute(_builder.Context);

        _builder.Calls.Should().Equal("Post.Dispose", "Opaque.Execute", "Styled.Execute");
    }

    [Fact]
    public void RemovesAPassWithoutDisposingIt()
    {
        RenderPipeline pipeline = _builder.Build("Opaque", "Post");
        IRenderPass post = _builder.Pass("Post");

        pipeline.Remove(post);
        pipeline.Execute(_builder.Context);

        _builder.Calls.Should().Equal("Opaque.Execute");
    }

    [Fact]
    public void ThrowsWhenTheAnchorPassIsMissing()
    {
        RenderPipeline pipeline = _builder.Build("Opaque");
        IRenderPass missing = _builder.Pass("Missing");
        IRenderPass fog = _builder.Pass("Fog");

        Action insert = () => pipeline.InsertBefore(missing, fog);

        insert.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DisposesEveryPass()
    {
        RenderPipeline pipeline = _builder.Build("Shadows", "Opaque");

        pipeline.Dispose();

        _builder.Calls.Should().Equal("Shadows.Dispose", "Opaque.Dispose");
    }

    [Fact]
    public void TimesEachPassOnTheGpuWhileProfiling()
    {
        RenderPipeline pipeline = _builder.WithProfile().Build("ShadowPass", "GeometryPass");

        pipeline.Execute(_builder.Context);

        _builder.Calls.Should().Equal(
            "Timer ShadowPass",
            "ShadowPass.Execute",
            "Timer end",
            "Timer GeometryPass",
            "GeometryPass.Execute",
            "Timer end");
    }

    [Fact]
    public void TimesEachPassOnTheCpuWhileProfiling()
    {
        RenderPipeline pipeline = _builder.WithProfile().WithWindow(1).Build("ShadowPass", "GeometryPass");

        pipeline.Prepare(_builder.Context);
        pipeline.Execute(_builder.Context);
        _builder.Profile.EndFrame();

        _builder.Profile.Passes.Select(pass => pass.Name).Should().Equal("ShadowPass", "GeometryPass");
        _builder.Profile.Passes.Should().OnlyContain(pass => pass.CpuMilliseconds >= 0);
    }

    [Fact]
    public void LeavesTheGpuUntimedWithoutAProfile()
    {
        RenderPipeline pipeline = _builder.WithCommands().Build("ShadowPass");

        pipeline.Execute(_builder.Context);

        _builder.Calls.Should().Equal("ShadowPass.Execute");
    }
}
