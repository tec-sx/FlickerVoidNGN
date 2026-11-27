using FlickerVoid.Engine.Rendering.Pipeline;

namespace FlickerVoid.Engine.Rendering.Tests.Pipeline;

/// <summary>Builds a render profile averaging over a window of the given number of frames.</summary>
internal sealed class RenderProfileTestsBuilder
{
    private int _window = RenderProfile.DefaultWindow;

    public RenderProfileTestsBuilder WithWindow(int frames)
    {
        _window = frames;
        return this;
    }

    public RenderProfile Build() => new(_window);
}
