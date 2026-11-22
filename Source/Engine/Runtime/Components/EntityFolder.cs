namespace FlickerVoid.Engine.Runtime.Components;

/// <summary>The folder an entity is filed under in its scene's outline, such as <c>Lights/Hall</c>; only the editor reads it.</summary>
public readonly record struct EntityFolder(string Path);
