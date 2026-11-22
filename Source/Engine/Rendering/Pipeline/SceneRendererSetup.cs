namespace FlickerVoid.Engine.Rendering.Pipeline;

/// <summary>
/// What modules add to the game's scene renderers as they are created, such as their own passes; a game service from
/// <c>AddRendering</c>, which the host applies to the renderer that draws the window.
/// </summary>
public sealed class SceneRendererSetup
{
    private readonly List<Action<SceneRenderer>> _setups = [];

    public void Add(Action<SceneRenderer> setup)
    {
        _setups.Add(setup);
    }

    /// <summary>Sets the renderer up as every module asked, in the order they asked.</summary>
    public void Apply(SceneRenderer renderer)
    {
        foreach (Action<SceneRenderer> setup in _setups)
        {
            setup(renderer);
        }
    }
}
