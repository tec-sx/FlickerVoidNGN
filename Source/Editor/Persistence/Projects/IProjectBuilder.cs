namespace FlickerVoid.Editor.Persistence.Projects;

/// <summary>Builds a project's game, which builds its content too.</summary>
public interface IProjectBuilder
{
    /// <summary>Builds the game's C# project and returns the built program (its target path); throws <see cref="ProjectBuildException"/> when the build fails.</summary>
    string Build(string gameProject);
}
