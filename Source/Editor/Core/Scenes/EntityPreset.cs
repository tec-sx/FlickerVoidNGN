using System.Globalization;
using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

using EntityData = FlickerVoid.Engine.Runtime.Scenes.EntityData;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>
/// A kind of entity the editor can add: a name, the components it starts with, each with its defaults, and the group it is listed
/// under in the Add menu, if any, and its icon there, named as in the shell's icon set. The entity is named uniquely in the scene,
/// because changes to the scene file match entities by name.
/// </summary>
public sealed record EntityPreset(string Name, IReadOnlyList<string> Components, string? Group = null)
{
    public static IReadOnlyList<EntityPreset> All { get; } =
    [
        new("Empty", ["transform"], "Empty") { Icon = "Empty" },
        new("Mesh", ["transform", "meshRenderer"], "Model") { Icon = "Model" },
        Greybox("Box", "[1, 1, 1]"),
        Greybox("Wall", "[4, 3, 0.2]"),
        Greybox("Floor", "[4, 0.2, 4]"),
        new("Point Light", ["transform", "pointLight"], "Light") { Icon = "PointLight" },
        new("Spot Light", ["transform", "spotLight"], "Light") { Icon = "SpotLight" },
        new("Irradiance Volume", ["transform", "irradianceVolume"], "Light") { Icon = "Grid" },
        new("Scene Mood", ["transform", "sceneMood"], "Light") { Icon = "Mood" },
        new("Camera", ["transform", "camera"], "Camera") { Icon = "Camera" },
    ];

    public string? Icon { get; init; }

    /// <summary>The starting JSON of this preset's components, over <see cref="Defaults"/>.</summary>
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();

    /// <summary>The starting JSON of components with required properties; the rest start as <c>{}</c>.</summary>
    private static readonly Dictionary<string, string> Defaults = new()
    {
        ["meshRenderer"] = """{ "mesh": "" }""",
    };

    /// <summary>A greybox of a typical size, to lay out a level before its models are made.</summary>
    private static EntityPreset Greybox(string name, string size)
    {
        Dictionary<string, string> values = new() { ["greybox"] = $$"""{ "size": {{size}} }""" };
        return new EntityPreset(name, ["transform", "greybox"], "Greybox") { Values = values, Icon = "Model" };
    }

    /// <summary>A mesh entity named after a model file in the content folder, which it draws.</summary>
    public static EntityPreset ForModel(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        JsonEncodedText mesh = JsonEncodedText.Encode(path);
        Dictionary<string, string> values = new() { ["meshRenderer"] = $$"""{ "mesh": "{{mesh}}" }""" };
        return new EntityPreset(name, ["transform", "meshRenderer"], "Model") { Values = values, Icon = "Model" };
    }

    /// <summary>The scene data of a new entity at <paramref name="position"/>, named apart from the entities in <paramref name="world"/>.</summary>
    public EntityData Create(World world, IEnumerable<Entity> entities, Vector3 position)
    {
        HashSet<string> taken = [];
        foreach (Entity entity in entities)
        {
            if (world.TryGet(entity, out EntityName name))
            {
                taken.Add(name.Value);
            }
        }

        string unique = Name;
        for (int number = 2; taken.Contains(unique); number++)
        {
            unique = $"{Name} {number}";
        }

        EntityData data = new() { Name = unique };
        foreach (string component in Components)
        {
            string? json = Values.GetValueOrDefault(component) ?? Defaults.GetValueOrDefault(component, "{}");
            if (component == "transform")
            {
                json = string.Create(CultureInfo.InvariantCulture, $$"""{ "position": [{{position.X}}, {{position.Y}}, {{position.Z}}] }""");
            }

            data.Components.Add(component, JsonDocument.Parse(json).RootElement.Clone());
        }

        return data;
    }

    public override string ToString() => Name;
}
