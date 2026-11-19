namespace FlickerVoid.Engine.Editor.Projects;

/// <summary>A project's game failed to build; the message holds the build's output.</summary>
public sealed class ProjectBuildException(string message) : Exception(message);
