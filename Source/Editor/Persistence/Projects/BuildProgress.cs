namespace FlickerVoid.Editor.Persistence.Projects;

/// <summary>How far a project's build has come: the projects built of those it builds (no total yet: 0), and what it does now.</summary>
public readonly record struct BuildProgress(int Built, int Total, string Status);
