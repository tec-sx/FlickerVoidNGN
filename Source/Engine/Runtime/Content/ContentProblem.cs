namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>What a <see cref="ContentCheck"/> found wrong in a content file, named relative to the content folder.</summary>
public sealed record ContentProblem(string Path, string Message)
{
    public override string ToString() => $"{Path}: {Message}";
}
