using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Engine.Runtime.Saves;
using FlickerVoid.Engine.Runtime.Scenes;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Content;

/// <summary>
/// Reads every text content file of a source folder as the game reads it, and lists what would fail or go unfound while playing:
/// files that can't be read, components no module registers, content files named that the folder lacks or of a kind their property
/// doesn't take, entities named that their scene lacks, and names or save ids two entities of a scene share. Games' tests run it.
/// </summary>
public sealed class ContentCheck
{
    /// <summary>Content files may hold comments and trailing commas, as the game's readers skip them.</summary>
    /// <summary>The folder of the built content that holds the engine's own, which content may name.</summary>
    public const string EngineFolder = "Engine";

    private static readonly JsonDocumentOptions ParseOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private readonly Game _game;
    private readonly string _folder;
    private readonly string _root;
    private readonly EntityTemplates _templates;
    private readonly List<ContentProblem> _problems = [];
    private string _path = "";

    private ContentCheck(Game game, string folder)
    {
        _game = game;
        _folder = Path.GetFullPath(folder);
        _root = _folder + Path.DirectorySeparatorChar;
        ContentManager content = new(_folder);
        _templates = new EntityTemplates(content, game.Spawner);
    }

    /// <summary>The problems of the folder's scenes, templates, data assets, tables and the kinds of files modules add to <see cref="Game.Schemas"/>, in path order; hidden files and folders are skipped.</summary>
    public static IReadOnlyList<ContentProblem> Run(Game game, string folder)
    {
        ContentCheck check = new(game, folder);
        List<string> paths = [];
        foreach (string fullPath in Directory.EnumerateFiles(check._folder, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(check._folder, fullPath);
            string path = relative.Replace('\\', '/');
            if (!IsHidden(path))
            {
                paths.Add(path);
            }
        }

        paths.Sort(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            check.CheckFile(path);
        }

        return check._problems;
    }

    private static bool IsHidden(string path)
    {
        foreach (string part in path.Split('/'))
        {
            if (part.StartsWith('.'))
            {
                return true;
            }
        }

        return false;
    }

    private void CheckFile(string path)
    {
        _path = path;
        string extension = Path.GetExtension(path);
        switch (extension)
        {
            case SceneData.Extension:
                CheckScene();
                break;
            case EntityTemplates.Extension:
                CheckTemplate();
                break;
            case DataAssets.AssetExtension:
                CheckAsset();
                break;
            case DataAssets.TableExtension:
                CheckTable();
                break;
            default:
                if (_game.Schemas.Files.TryGetValue(extension, out Func<JsonTypeInfo>? typeInfo))
                {
                    JsonTypeInfo fileType = typeInfo();
                    CheckValue(fileType);
                }

                break;
        }
    }

    private void CheckScene()
    {
        if (Parse() is not { } file || Read(file, RuntimeJsonContext.Default.SceneData, null) is not SceneData scene)
        {
            return;
        }

        HashSet<string> names = [];
        foreach (EntityData entity in scene.Entities)
        {
            if (entity.Name.Length > 0 && !names.Add(entity.Name))
            {
                Report($"Two entities are named {entity.Name}; what names an entity finds the first.");
            }
        }

        HashSet<string> ids = [];
        foreach (EntityData data in scene.Entities)
        {
            if (Expand(data) is not { } entity)
            {
                continue;
            }

            if (entity.Parent is { Length: > 0 } parent && !names.Contains(parent))
            {
                Report($"{entity.Name} is nested under {parent}, which the scene lacks.");
            }

            foreach ((string name, JsonElement value) in entity.Components)
            {
                string subject = $"{entity.Name}'s {name}";
                if (CheckComponent(subject, name, value) is not { } component)
                {
                    continue;
                }

                CheckEntityReferences(subject, component, value, names);
                if (component.TypeInfo.Type == typeof(Saveable))
                {
                    CheckSaveId(value, ids);
                }
            }
        }
    }

    /// <summary>The entity with its template's components under its own, or null, reported, when the template can't be found or read.</summary>
    private EntityData? Expand(EntityData entity)
    {
        if (entity.Template is not { } template)
        {
            return entity;
        }

        if (!CheckNamedFile(template, $"{entity.Name}'s template", [EntityTemplates.Extension]))
        {
            return null;
        }

        try
        {
            return _templates.Expand(entity);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or IOException or ArgumentException)
        {
            Report($"{entity.Name}'s template {template} can't be read: {exception.Message}");
            return null;
        }
    }

    private void CheckTemplate()
    {
        if (Parse() is not { } file || Read(file, RuntimeJsonContext.Default.EntityTemplateFile, null) is not EntityTemplateFile template)
        {
            return;
        }

        if (template.Base is { } under)
        {
            CheckNamedFile(under, "base", [EntityTemplates.Extension]);
        }

        foreach ((string name, JsonElement value) in template.Components)
        {
            CheckComponent(name, name, value);
        }
    }

    /// <summary>The component's registration, or null, reported, when no module registers it or its value can't be read; checks the files it names.</summary>
    private SceneComponent? CheckComponent(string subject, string name, JsonElement value)
    {
        SceneComponent? component = FindComponent(name);
        if (component is null)
        {
            Report($"No module registers {subject}.");
            return null;
        }

        if (Read(value, component.TypeInfo, subject) is null)
        {
            return null;
        }

        CheckFiles(component.TypeInfo, value, subject);
        return component;
    }

    private void CheckEntityReferences(string subject, SceneComponent component, JsonElement value, HashSet<string> names)
    {
        foreach (string target in EntityReferences.Find(component.TypeInfo, value))
        {
            if (!names.Contains(target))
            {
                Report($"{subject} names {target}, which the scene lacks.");
            }
        }
    }

    private void CheckSaveId(JsonElement value, HashSet<string> ids)
    {
        Saveable saveable = value.Deserialize(RuntimeJsonContext.Default.Saveable);
        if (saveable.Id.Length > 0 && !ids.Add(saveable.Id))
        {
            Report($"Two entities are saved as {saveable.Id}; a save keeps one of them.");
        }
    }

    private SceneComponent? FindComponent(string name)
    {
        foreach (SceneComponent component in _game.Spawner.Components)
        {
            if (component.Name == name)
            {
                return component;
            }
        }

        return null;
    }

    private void CheckAsset()
    {
        if (ParseObject() is not { } file)
        {
            return;
        }

        JsonObject node = JsonObject.Create(file)!;
        object asset;
        try
        {
            asset = _game.Data.ReadAsset(_path, node);
        }
        catch (InvalidDataException exception)
        {
            Report(exception.Message);
            return;
        }

        Type type = asset.GetType();
        JsonTypeInfo typeInfo = _game.Data.Options.GetTypeInfo(type);
        CheckFiles(typeInfo, file, "");
    }

    private void CheckTable()
    {
        if (ParseObject() is not { } file)
        {
            return;
        }

        JsonObject node = JsonObject.Create(file)!;
        Type type;
        try
        {
            _game.Data.ReadRows(_path, node, out type);
        }
        catch (InvalidDataException exception)
        {
            Report(exception.Message);
            return;
        }

        JsonTypeInfo typeInfo = _game.Data.Options.GetTypeInfo(type);
        if (!file.TryGetProperty("rows", out JsonElement rows) || rows.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty row in rows.EnumerateObject())
        {
            CheckFiles(typeInfo, row.Value, $"row {row.Name}");
        }
    }

    private void CheckValue(JsonTypeInfo typeInfo)
    {
        if (Parse() is not { } file || Read(file, typeInfo, null) is null)
        {
            return;
        }

        CheckFiles(typeInfo, file, "");
    }

    /// <summary>The file as JSON, or null, reported, when it isn't.</summary>
    private JsonElement? Parse()
    {
        string fullPath = Path.Combine(_folder, _path);
        byte[] bytes = File.ReadAllBytes(fullPath);
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, ParseOptions);
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            Report($"It can't be read: {exception.Message}");
            return null;
        }
    }

    private JsonElement? ParseObject()
    {
        if (Parse() is not { } file)
        {
            return null;
        }

        if (file.ValueKind != JsonValueKind.Object)
        {
            Report("It holds no object.");
            return null;
        }

        return file;
    }

    /// <summary>The value as the type, or null, reported as <paramref name="subject"/>'s or else the file's, when it can't be read.</summary>
    private object? Read(JsonElement value, JsonTypeInfo typeInfo, string? subject)
    {
        try
        {
            return value.Deserialize(typeInfo);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            if (subject is null)
            {
                Report($"It can't be read: {exception.Message}");
            }
            else
            {
                Report($"{subject} can't be read: {exception.Message}");
            }

            return null;
        }
    }

    /// <summary>Checks every content file the value names, at any depth, through the properties marked <see cref="ContentFileAttribute"/>.</summary>
    private void CheckFiles(JsonTypeInfo typeInfo, JsonElement value, string at)
    {
        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.Object when value.ValueKind == JsonValueKind.Object:
                CheckProperties(typeInfo, value, at);
                break;
            case JsonTypeInfoKind.Enumerable when value.ValueKind == JsonValueKind.Array:
                if (FindTypeInfo(typeInfo.Options, typeInfo.ElementType) is { } item)
                {
                    int index = 0;
                    foreach (JsonElement element in value.EnumerateArray())
                    {
                        CheckFiles(item, element, $"{at}[{index}]");
                        index++;
                    }
                }

                break;
            case JsonTypeInfoKind.Dictionary when value.ValueKind == JsonValueKind.Object:
                if (FindTypeInfo(typeInfo.Options, typeInfo.ElementType) is { } entry)
                {
                    foreach (JsonProperty element in value.EnumerateObject())
                    {
                        CheckFiles(entry, element.Value, Join(at, element.Name));
                    }
                }

                break;
        }
    }

    private void CheckProperties(JsonTypeInfo typeInfo, JsonElement value, string at)
    {
        JsonTypeInfo shape = GetShape(typeInfo, value);
        foreach (JsonPropertyInfo property in shape.Properties)
        {
            if (!value.TryGetProperty(property.Name, out JsonElement member))
            {
                continue;
            }

            string where = Join(at, property.Name);
            if (GetContentFile(property) is { } file)
            {
                CheckNamedFiles(member, where, file.Extensions);
            }
            else if (FindTypeInfo(shape.Options, property.PropertyType) is { } memberType)
            {
                CheckFiles(memberType, member, where);
            }
        }
    }

    /// <summary>The type info of the derived type the value names with its type discriminator, or else the type's own.</summary>
    private static JsonTypeInfo GetShape(JsonTypeInfo typeInfo, JsonElement value)
    {
        if (typeInfo.PolymorphismOptions is not { } polymorphism)
        {
            return typeInfo;
        }

        string property = polymorphism.TypeDiscriminatorPropertyName;
        if (!value.TryGetProperty(property, out JsonElement discriminator) || discriminator.ValueKind != JsonValueKind.String)
        {
            return typeInfo;
        }

        string? name = discriminator.GetString();
        foreach (JsonDerivedType derived in polymorphism.DerivedTypes)
        {
            if (derived.TypeDiscriminator is string named && named == name && FindTypeInfo(typeInfo.Options, derived.DerivedType) is { } shape)
            {
                return shape;
            }
        }

        return typeInfo;
    }

    /// <summary>The type info of the type, or of the value type a nullable one holds; null when the options know none.</summary>
    private static JsonTypeInfo? FindTypeInfo(JsonSerializerOptions options, Type? type)
    {
        if (type is null)
        {
            return null;
        }

        Type held = Nullable.GetUnderlyingType(type) ?? type;
        options.TryGetTypeInfo(held, out JsonTypeInfo? typeInfo);
        return typeInfo;
    }

    private static ContentFileAttribute? GetContentFile(JsonPropertyInfo property)
    {
        if (property.AttributeProvider is not { } provider)
        {
            return null;
        }

        object[] attributes = provider.GetCustomAttributes(typeof(ContentFileAttribute), inherit: true);
        if (attributes is [ContentFileAttribute attribute, ..])
        {
            return attribute;
        }

        return null;
    }

    private void CheckNamedFiles(JsonElement value, string at, IReadOnlyList<string> extensions)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string file = value.GetString()!;
            CheckNamedFile(file, at, extensions);
            return;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string file = item.GetString()!;
                CheckNamedFile(file, $"{at}[{index}]", extensions);
            }

            index++;
        }
    }

    /// <summary>Whether the file is in the folder with one of the extensions, when any are given; reported when it isn't, and never for an empty name.</summary>
    private bool CheckNamedFile(string file, string at, IReadOnlyList<string> extensions)
    {
        if (file.Length == 0)
        {
            return true;
        }

        string combined = Path.Combine(_folder, file);
        string fullPath = Path.GetFullPath(combined);
        bool inFolder = fullPath.StartsWith(_root, StringComparison.Ordinal) && File.Exists(fullPath);
        if (!inFolder && !IsEngineContent(file))
        {
            Report($"{at} names {file}, which isn't in the content folder.");
            return false;
        }

        string extension = Path.GetExtension(file);
        if (extensions.Count > 0 && !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            string kinds = string.Join(" or ", extensions);
            Report($"{at} names {file}, which isn't a {kinds} file.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the file is the engine's own content (<c>Engine/Rendering/Masters/Painted.fvmaster</c>), which the content build
    /// puts under <see cref="EngineFolder"/> beside the program rather than in a game's source folder, named by its source as the
    /// game's own content is (<c>Engine/Rendering/Reflections/Quarry.jpg</c>, built as <c>Quarry.dds</c>).
    /// </summary>
    private static bool IsEngineContent(string file)
    {
        if (!file.StartsWith(EngineFolder + "/", StringComparison.Ordinal))
        {
            return false;
        }

        string built = Path.Combine(AppContext.BaseDirectory, "Content", file);
        if (File.Exists(built))
        {
            return true;
        }

        // A source the build turns into a file of another kind (an image into a .dds) is named by its source.
        string folder = Path.GetDirectoryName(built)!;
        if (!Directory.Exists(folder))
        {
            return false;
        }

        string pattern = Path.GetFileNameWithoutExtension(built) + ".*";
        return Directory.EnumerateFiles(folder, pattern).Any();
    }

    private static string Join(string at, string name)
    {
        if (at.Length == 0)
        {
            return name;
        }

        return $"{at}.{name}";
    }

    private void Report(string message)
    {
        ContentProblem problem = new(_path, message);
        _problems.Add(problem);
    }
}
