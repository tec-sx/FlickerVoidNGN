namespace FlickerVoid.Tools.Builder;

/// <summary>What a build did with each source file, by its path under the source folder.</summary>
public sealed record BuildReport(
    IReadOnlyList<string> Built,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<BuildFailure> Failed,
    IReadOnlyList<string> Removed);
