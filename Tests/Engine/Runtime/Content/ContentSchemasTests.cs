using System.Text.Json.Nodes;

using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

public sealed class ContentSchemasTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "FlickerVoidSchemaTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void WritesEachSchemaIntoTheSchemaFolderOfASourceFolder()
    {
        ContentSchemas schemas = new();
        schemas.Add("mood", static () => new JsonObject { ["type"] = "object" });

        schemas.Write(_folder);

        string path = Path.Combine(_folder, ContentSchemas.FolderName, "mood.schema.json");
        string json = File.ReadAllText(path);
        JsonNode? schema = JsonNode.Parse(json);
        schema!["type"]!.GetValue<string>().Should().Be("object");
    }

    [Fact]
    public void LeavesAnUnchangedSchemaFileAlone()
    {
        ContentSchemas schemas = new();
        schemas.Add("mood", static () => new JsonObject { ["type"] = "object" });
        schemas.Write(_folder);
        string path = Path.Combine(_folder, ContentSchemas.FolderName, "mood.schema.json");
        DateTime past = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, past);

        schemas.Write(_folder);

        File.GetLastWriteTimeUtc(path).Should().Be(past, "rewriting it would wake the content builder for nothing");
    }
}
