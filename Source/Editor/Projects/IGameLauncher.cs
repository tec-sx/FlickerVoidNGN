namespace FlickerVoid.Engine.Editor.Projects;

/// <summary>Starts a built game in a process of its own.</summary>
public interface IGameLauncher
{
    /// <summary>Starts <paramref name="program"/> (a built <c>.dll</c> or executable) with the arguments and returns at once.</summary>
    void Launch(string program, IReadOnlyList<string> arguments);
}
