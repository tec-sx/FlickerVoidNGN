using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>
/// A camera's lens (a <c>.fvlens</c> file, which the scene's main camera names): the values that belong to the camera rather
/// than the hour, such as bloom, the tonemapper, the canvas and the depth fade, laid under the scene's mood. It holds only the
/// values its file names, and only the lens's own.
/// </summary>
[JsonConverter(typeof(CameraLensJsonConverter))]
public sealed class CameraLens(MoodOverride values)
{
    public const string Extension = ".fvlens";

    /// <summary>The values the lens sets.</summary>
    public MoodOverride Values { get; } = values;

    /// <summary>The schema of lens files: a mood file's, with only the lens's values.</summary>
    public static JsonObject CreateSchema()
    {
        JsonObject schema = JsonSchemas.CreateFile(RenderingJsonContext.Default.LightingMood);
        JsonObject properties = schema["properties"]!.AsObject();
        List<string> names = [.. properties.Select(property => property.Key)];
        foreach (string name in names)
        {
            MoodValue? value = MoodValues.Find(name);
            if (value is not null && value.Owner != MoodOwner.Lens)
            {
                properties.Remove(name);
            }
        }

        return schema;
    }
}
