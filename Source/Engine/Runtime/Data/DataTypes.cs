using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Data;

/// <summary>
/// The C# types data files hold, registered by name: data asset types (<c>.fvdata</c>, one object), table row types
/// (<c>.fvtable</c>) and the derived types of polymorphic bases such as fragments, conditions and effects, which files name with
/// <c>$type</c>, as Unreal's instanced objects. Modules add the source-generated contexts that describe their types, so reading
/// stays free of reflection.
/// </summary>
public sealed class DataTypes
{
    public const string TypeProperty = "type";

    public const string DerivedTypeProperty = "$type";

    private readonly List<IJsonTypeInfoResolver> _resolvers = [];
    private readonly Dictionary<string, Type> _assets = [];
    private readonly Dictionary<string, Type> _rows = [];
    private readonly Dictionary<Type, List<JsonDerivedType>> _derived = [];
    private JsonSerializerOptions? _options;

    /// <summary>The options that read every registered type; registering after the first read throws.</summary>
    public JsonSerializerOptions Options => _options ??= CreateOptions();

    public IReadOnlyDictionary<string, Type> AssetTypes => _assets;

    public IReadOnlyDictionary<string, Type> RowTypes => _rows;

    /// <summary>Adds a source-generated context (or other resolver) that describes registered types.</summary>
    public void AddResolver(IJsonTypeInfoResolver resolver)
    {
        ThrowIfInUse();
        _resolvers.Add(resolver);
    }

    /// <summary>Lets <c>.fvdata</c> files hold a <typeparamref name="T"/>, named <paramref name="name"/> in their <c>type</c>.</summary>
    public void Register<T>(string name)
        where T : class
    {
        ThrowIfInUse();
        _assets.Add(name, typeof(T));
    }

    /// <summary>Lets <c>.fvtable</c> files hold rows of <typeparamref name="TRow"/>, named <paramref name="name"/> in their <c>type</c>.</summary>
    public void RegisterRow<TRow>(string name)
        where TRow : class
    {
        ThrowIfInUse();
        _rows.Add(name, typeof(TRow));
    }

    /// <summary>Lets a <typeparamref name="TBase"/> value in a data file be a <typeparamref name="TDerived"/>, named by <c>$type</c>.</summary>
    public void RegisterDerived<TBase, TDerived>(string name)
        where TDerived : TBase
    {
        ThrowIfInUse();
        if (!_derived.TryGetValue(typeof(TBase), out List<JsonDerivedType>? derived))
        {
            derived = [];
            _derived.Add(typeof(TBase), derived);
        }

        if (derived.Any(type => (string?)type.TypeDiscriminator == name))
        {
            throw new ArgumentException($"{typeof(TBase).Name} already has a type named {name}.", nameof(name));
        }

        derived.Add(new JsonDerivedType(typeof(TDerived), name));
    }

    /// <summary>The data asset a <c>.fvdata</c> file holds, as the type its <c>type</c> names; <paramref name="path"/> names the file in errors.</summary>
    public object ReadAsset(string path, JsonObject file)
    {
        Type type = GetType(path, file, _assets);
        JsonObject body = new();
        foreach ((string name, JsonNode? value) in file)
        {
            if (name is not TypeProperty and not JsonSchemas.SchemaProperty)
            {
                body[name] = value?.DeepClone();
            }
        }

        JsonTypeInfo typeInfo = Options.GetTypeInfo(type);
        try
        {
            return body.Deserialize(typeInfo) ?? throw new InvalidDataException($"{path} holds no data.");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new InvalidDataException($"{path} can't be read: {exception.Message}", exception);
        }
    }

    /// <summary>A data asset as a <c>.fvdata</c> file: its schema, the name its type is registered under, then its values.</summary>
    public byte[] WriteAsset(object asset, string? schema)
    {
        Type type = asset.GetType();
        string name = _assets.First(pair => pair.Value == type).Key;
        JsonTypeInfo typeInfo = Options.GetTypeInfo(type);
        JsonNode? values = JsonSerializer.SerializeToNode(asset, typeInfo);
        JsonObject file = [];
        if (schema is not null)
        {
            file[JsonSchemas.SchemaProperty] = schema;
        }

        file[TypeProperty] = name;
        if (values is JsonObject properties)
        {
            foreach ((string property, JsonNode? value) in properties)
            {
                file[property] = value?.DeepClone();
            }
        }

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
        {
            file.WriteTo(writer);
        }

        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    /// <summary>The registered type a file names in <c>type</c>.</summary>
    internal static Type GetType(string path, JsonObject file, IReadOnlyDictionary<string, Type> types)
    {
        string? name = file[TypeProperty]?.GetValue<string>();
        if (name is null)
        {
            throw new InvalidDataException($"{path} names no \"{TypeProperty}\".");
        }

        if (!types.TryGetValue(name, out Type? type))
        {
            throw new InvalidDataException($"{path} has the unknown type {name}.");
        }

        return type;
    }

    /// <summary>The JSON Schema of data asset files: one of the registered types, named by <c>type</c>.</summary>
    public JsonObject GetAssetSchema()
    {
        JsonArray choices = [];
        foreach ((string name, Type type) in _assets)
        {
            JsonTypeInfo typeInfo = Options.GetTypeInfo(type);
            JsonObject schema = JsonSchemas.CreateFile(typeInfo);
            AddTypeName(schema, name);
            choices.Add((JsonNode)schema);
        }

        return CreateChoice(choices);
    }

    /// <summary>The JSON Schema of data table files: rows of one of the registered row types, named by <c>type</c>.</summary>
    public JsonObject GetTableSchema()
    {
        JsonArray choices = [];
        foreach ((string name, Type type) in _rows)
        {
            JsonTypeInfo typeInfo = Options.GetTypeInfo(type);
            JsonObject row = JsonSchemas.Create(typeInfo);
            JsonObject rows = new() { ["type"] = "object", ["additionalProperties"] = row };
            JsonObject properties = new() { [JsonSchemas.SchemaProperty] = new JsonObject { ["type"] = "string" }, ["rows"] = rows };
            JsonObject schema = new() { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
            AddTypeName(schema, name);
            choices.Add((JsonNode)schema);
        }

        return CreateChoice(choices);
    }

    /// <summary>One of the choices; any object while nothing is registered, since an empty anyOf matches nothing.</summary>
    private static JsonObject CreateChoice(JsonArray choices)
    {
        if (choices.Count == 0)
        {
            return new JsonObject { ["type"] = "object" };
        }

        return new JsonObject { ["anyOf"] = choices };
    }

    private static void AddTypeName(JsonObject schema, string name)
    {
        JsonObject properties = schema["properties"]!.AsObject();
        properties[TypeProperty] = new JsonObject { ["const"] = name };
        schema["required"] = new JsonArray(TypeProperty);
    }

    private JsonSerializerOptions CreateOptions()
    {
        IJsonTypeInfoResolver combined = JsonTypeInfoResolver.Combine([.. _resolvers]);
        IJsonTypeInfoResolver resolver = combined.WithAddedModifier(AddDerivedTypes);
        JsonSerializerOptions options = new()
        {
            TypeInfoResolver = resolver,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            Converters = { new Vector3JsonConverter(), new QuaternionJsonConverter(), new TagJsonConverter() },
        };
        options.MakeReadOnly();
        return options;
    }

    private void AddDerivedTypes(JsonTypeInfo typeInfo)
    {
        if (!_derived.TryGetValue(typeInfo.Type, out List<JsonDerivedType>? derived))
        {
            return;
        }

        JsonPolymorphismOptions polymorphism = new()
        {
            TypeDiscriminatorPropertyName = DerivedTypeProperty,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        foreach (JsonDerivedType type in derived)
        {
            polymorphism.DerivedTypes.Add(type);
        }

        typeInfo.PolymorphismOptions = polymorphism;
    }

    private void ThrowIfInUse()
    {
        if (_options is not null)
        {
            throw new InvalidOperationException("Data types must be registered before the first data file is read.");
        }
    }
}
