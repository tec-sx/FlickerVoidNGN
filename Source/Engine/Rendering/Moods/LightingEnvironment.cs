using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

using FlickerVoid.Engine.Rendering.Lighting;
using FlickerVoid.Engine.Runtime.Curves;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>
/// A place's light and air over the hours of its day: a curve for each environment value of the mood it draws, read
/// against a day of 24 hours, so the last key before midnight leads on to the first one after it.
/// </summary>
public sealed class LightingEnvironment : IJsonOnDeserialized
{
    public const string Extension = ".fvenv";

    /// <summary>The hours of a day, which every curve wraps around.</summary>
    public const float DayHours = 24;

    /// <summary>The schema the file names for text editors; the game ignores it.</summary>
    [JsonPropertyName(JsonSchemas.SchemaProperty)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; set; }

    /// <summary>The curve of each number the environment draws, by the mood value's name.</summary>
    public Dictionary<string, Curve> Numbers { get; set; } = [];

    /// <summary>The curve of each color the environment draws, by the mood value's name.</summary>
    public Dictionary<string, ColorCurve> Colors { get; set; } = [];

    /// <summary>Writes every curve's value at an hour of the day onto a mood and leaves the mood's other values alone.</summary>
    public void Apply(float hour, LightingMood mood)
    {
        foreach ((string name, Curve curve) in Numbers)
        {
            if (MoodValues.Find(name) is MoodNumber number)
            {
                float value = curve.Evaluate(hour, DayHours);
                number.Set(mood, value);
            }
        }

        foreach ((string name, ColorCurve curve) in Colors)
        {
            if (MoodValues.Find(name) is MoodColor color)
            {
                Vector3 value = curve.Evaluate(hour, DayHours);
                color.Set(mood, value);
            }
        }
    }

    /// <summary>Refuses a file with a curve for what is not the environment's to draw, so a misspelt name is not silently ignored.</summary>
    void IJsonOnDeserialized.OnDeserialized()
    {
        foreach (string name in Numbers.Keys)
        {
            Require<MoodNumber>(name, "number");
        }

        foreach (string name in Colors.Keys)
        {
            Require<MoodColor>(name, "color");
        }
    }

    private static void Require<TValue>(string name, string kind)
        where TValue : MoodValue
    {
        MoodValue? value = MoodValues.Find(name);
        if (value is null)
        {
            throw new JsonException($"No mood value is named '{name}'.");
        }

        if (value.Owner != MoodOwner.Environment)
        {
            throw new JsonException($"'{name}' belongs to the lens, so an environment cannot draw it.");
        }

        if (value is not TValue)
        {
            throw new JsonException($"'{name}' is not a {kind}, so it cannot be drawn as one.");
        }
    }
}
