namespace FlickerVoid.Tools.Builder;

/// <summary>A source file that failed to build, and why.</summary>
public sealed record BuildFailure(string Path, string Message);
