using System.Text.Json.Serialization;

namespace FlickerVoid.Editor.Tests.Data;

/// <summary>A data asset type the main window tests register, as a project's game module would.</summary>
public sealed class Lantern
{
    public int Hours { get; set; } = 4;
}

[JsonSerializable(typeof(Lantern))]
internal sealed partial class LanternJsonContext : JsonSerializerContext;
