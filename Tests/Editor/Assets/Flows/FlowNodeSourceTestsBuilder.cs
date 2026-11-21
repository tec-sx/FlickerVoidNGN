namespace FlickerVoid.Editor.Assets.Tests.Flows;

/// <summary>Builds a temporary code folder holding a game project with a JSON context, where flow node classes are written.</summary>
internal sealed class FlowNodeSourceTestsBuilder : IDisposable
{
    public const string Context = """
        using System.Text.Json.Serialization;

        namespace Cellar.Gameplay.Serialization;

        /// <summary>The game's JSON.</summary>
        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
        [JsonSerializable(typeof(Barrel))]
        public sealed partial class CellarJsonContext : JsonSerializerContext;
        """;

    public FlowNodeSourceTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        Root = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public FlowNodeSourceTestsBuilder WithFile(string path, string text = "")
    {
        string fullPath = Path.Combine(Root, path);
        string folder = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(fullPath, text);
        return this;
    }

    /// <summary>The Cellar.Gameplay project in the Gameplay folder, with its JSON context and a World/Doors folder.</summary>
    public FlowNodeSourceTestsBuilder WithProject()
    {
        WithFile("Gameplay/Cellar.Gameplay.csproj", "<Project />");
        WithFile("Gameplay/Serialization/CellarJsonContext.cs", Context);
        Directory.CreateDirectory(Path.Combine(Root, "Gameplay", "World", "Doors"));
        Directory.CreateDirectory(Path.Combine(Root, "Gameplay", "bin", "Debug"));
        return this;
    }

    public string GetPath(string path) => Path.Combine(Root, path);

    public string ReadFile(string path) => File.ReadAllText(GetPath(path));

    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
    }
}
