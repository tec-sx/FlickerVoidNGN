using System.Globalization;
using System.Numerics;
using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

using EntityData = FlickerVoid.Engine.Runtime.Scenes.EntityData;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>
/// A kind of entity the editor can add: a name and the components it starts with, each with its defaults. The entity is named
/// uniquely in the scene, because changes to the scene file match entities by name.
/// </summary>
public sealed record EntityPreset(string Name, IReadOnlyList<string> Components)
{
    public static IReadOnlyList<EntityPreset> All { get; } =
    [
        new("Empty", ["transform"]),
        new("Mesh", ["transform", "meshRenderer"]),
        new("Point Light", ["transform", "pointLight"]),
        new("Spot Light", ["transform", "spotLight"]),
        new("Camera", ["transform", "camera"]),
        new("Irradiance Volume", ["transform", "irradianceVolume"]),
        new("Scene Mood", ["transform", "sceneMood"]),
    ];

    /// <summary>The starting JSON of components with required properties; the rest start as <c>{}</c>.</summary>
    private static readonly Dictionary<string, string> Defaults = new()
    {
        ["meshRenderer"] = """{ "mesh": "" }""",
    };

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
            string json = Defaults.GetValueOrDefault(component, "{}");
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
