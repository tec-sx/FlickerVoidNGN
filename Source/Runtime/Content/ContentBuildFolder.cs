namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>A source content folder and the folder of the running game it is built into.</summary>
public sealed record ContentBuildFolder(string Source, string Output);
