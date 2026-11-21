namespace FlickerVoid.Engine.Hosting.Tests;

/// <summary>Builds a list of substitute overlays, drawn in the order they were added.</summary>
internal sealed class FrameOverlaysTestsBuilder
{
    public List<IFrameOverlay> Overlays { get; } = [];

    public FrameOverlaysTestsBuilder WithOverlays(int count)
    {
        for (int index = 0; index < count; index++)
        {
            Overlays.Add(Substitute.For<IFrameOverlay>());
        }

        return this;
    }

    public FrameOverlays Build() => new(Overlays);
}
