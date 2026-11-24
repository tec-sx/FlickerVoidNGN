namespace FlickerVoid.Editor.Projects;

/// <summary>A project that failed to open, with the exception's log: the build's output when the build failed.</summary>
public sealed record ProjectOpenFailure(string Project, string Log);
