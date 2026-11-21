namespace FlickerVoid.Engine.Runtime.Loop;

/// <summary>
/// A project's part of a game: it registers the project's components, systems, lifecycle hooks and schemas. The launcher adds its
/// modules to the game, and the editor finds them in the built game, so scenes edited there know the project's components.
/// </summary>
public interface IGameModule
{
    void Add(Game game);
}
