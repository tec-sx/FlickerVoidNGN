using System.Numerics;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Runtime.Serialization;

/// <summary>
/// Describes content file types as JSON Schemas from the same source-generated metadata that reads them, so a text editor
/// can check and complete a file against what the game will actually accept.
/// </summary>
public static class JsonSchemas
{
    /// <summary>The property a content file names its schema with; files may carry it, readers ignore it.</summary>
    public const string SchemaProperty = "$schema";

    private static readonly JsonSchemaExporterOptions Options = new()
    {
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = Transform,
    };

    /// <summary>The schema of a value of the type, as it appears inside a file.</summary>
    public static JsonObject Create(JsonTypeInfo typeInfo)
    {
        JsonNode schema = typeInfo.GetJsonSchemaAsNode(Options);
        return schema.AsObject();
    }

    /// <summary>The schema of a whole file holding the type: it may name its schema and nothing outside the type.</summary>
    public static JsonObject CreateFile(JsonTypeInfo typeInfo)
    {
        JsonObject schema = Create(typeInfo);
        JsonObject properties = schema["properties"]?.AsObject() ?? [];
        properties[SchemaProperty] = new JsonObject { ["type"] = "string" };
        schema["properties"] = properties;
        schema["additionalProperties"] = false;
        return schema;
    }

    private static JsonNode Transform(JsonSchemaExporterContext context, JsonNode schema)
    {
        Type type = context.TypeInfo.Type;
        if (type == typeof(Vector2))
        {
            return CreateNumberArray(2);
        }

        if (type == typeof(Vector3))
        {
            return CreateNumberArray(3);
        }

        if (type == typeof(Quaternion))
        {
            return CreateNumberArray(4);
        }

        if (type == typeof(Tag))
        {
            return new JsonObject { ["type"] = "string" };
        }

        if (type == typeof(SolidMesh))
        {
            return CreateSolid();
        }

        return schema;
    }

    /// <summary>A solid's points, each three numbers, and its faces, each the numbers of three points or more.</summary>
    private static JsonObject CreateSolid()
    {
        JsonObject face = new()
        {
            ["type"] = "array",
            ["items"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
            ["minItems"] = 3,
        };
        JsonObject properties = new()
        {
            ["points"] = new JsonObject { ["type"] = "array", ["items"] = CreateNumberArray(3) },
            ["faces"] = new JsonObject { ["type"] = "array", ["items"] = face },
        };
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = new JsonArray("points", "faces"),
            ["additionalProperties"] = false,
        };
    }

    private static JsonObject CreateNumberArray(int count)
    {
        return new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject { ["type"] = "number" },
            ["minItems"] = count,
            ["maxItems"] = count,
        };
    }
}
