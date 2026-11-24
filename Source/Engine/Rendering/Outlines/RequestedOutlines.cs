namespace FlickerVoid.Engine.Rendering.Outlines;

/// <summary>The outline requests an entity holds, in the order made, and the outline it had before the first; kept by <see cref="OutlineRequests"/>.</summary>
public struct RequestedOutlines
{
    public RequestedOutlines()
    {
    }

    public List<OutlineRequest> Requests { get; set; } = [];

    /// <summary>The outline the entity had before any request, such as one set in its scene, given back when the last is withdrawn.</summary>
    public Outline? Authored { get; set; }
}
