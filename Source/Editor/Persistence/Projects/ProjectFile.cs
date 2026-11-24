using System.Text.Json.Serialization;

namespace FlickerVoid.Editor.Persistence.Projects;

/// <summary>A project file (<c>Name.fvproject</c>): the game that builds the project's content, the scene the editor opens first and its splash.</summary>
public sealed class ProjectFile
{
    /// <summary>The schema the file names for text editors; the editor ignores it.</summary>
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    /// <summary>The game's C# project, relative to the project file; building it builds the content.</summary>
    public string? Game { get; set; }

    /// <summary>The scene to open, as a content path; none opens an empty world.</summary>
    public string? StartupScene { get; set; }

    /// <summary>The image the editor's splash shows while the project builds, relative to the project file; none shows a star.</summary>
    public string? Splash { get; set; }
}
