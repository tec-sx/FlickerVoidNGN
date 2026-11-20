namespace FlickerVoid.Editor.Persistence.Projects;

/// <summary>A project the picker lists: its name and its project file.</summary>
public sealed record ProjectEntry(string Name, string File);
