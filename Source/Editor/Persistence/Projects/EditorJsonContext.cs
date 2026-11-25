using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Editor.Persistence.Layout;

namespace FlickerVoid.Editor.Persistence.Projects;

/// <summary>Source-generated JSON for the editor's own files: comments and trailing commas allowed, unknown properties rejected.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true)]
[JsonSerializable(typeof(ProjectFile))]
[JsonSerializable(typeof(RecentProjectsFile))]
[JsonSerializable(typeof(EditorLayoutFile))]
public sealed partial class EditorJsonContext : JsonSerializerContext;
