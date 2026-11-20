namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>A source content folder and the folder of the running game it is built into; engine folders hold the engine's own content, such as its shaders.</summary>
public sealed record ContentBuildFolder(string Source, string Output, bool Engine = false);
