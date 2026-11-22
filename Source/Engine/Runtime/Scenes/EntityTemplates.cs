using System.Text.Json;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Serialization;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>
/// Entity templates (<c>.fventity</c> files): the components and values an entity starts with, like a Blueprint carrying looks and
/// data. Scenes name one per entity and keep only what differs from it, and game code spawns them.
/// </summary>
public sealed class EntityTemplates
{
    public const string Extension = ".fventity";

    private const string TransformName = "transform";

    private readonly IContentManager _content;
    private readonly ISceneSpawner _spawner;

    public EntityTemplates(IContentManager content, ISceneSpawner spawner)
    {
        _content = content;
        _spawner = spawner;
    }

    /// <summary>Creates and starts an entity from the template at <paramref name="path"/>, placed at <paramref name="transform"/>.</summary>
    public Entity Spawn(string path, World world, in Transform transform)
    {
        JsonElement placed = JsonSerializer.SerializeToElement(transform, RuntimeJsonContext.Default.Transform);
        EntityData data = new() { Template = path, Components = { [TransformName] = placed } };
        EntityData expanded = Expand(data);
        expanded.Components[TransformName] = placed;
        return _spawner.Create(expanded, world);
    }

    /// <summary>The entity with its template's components under its own; the same data when it names no template.</summary>
    public EntityData Expand(EntityData data)
    {
        if (data.Template is not { } template)
        {
            return data;
        }

        Dictionary<string, JsonElement> components = GetComponents(template);
        foreach ((string name, JsonElement value) in data.Components)
        {
            if (components.TryGetValue(name, out JsonElement under))
            {
                components[name] = JsonMerge.Merge(under, value);
            }
            else
            {
                components[name] = value;
            }
        }

        return new EntityData { Name = data.Name, Template = template, Folder = data.Folder, Components = components };
    }

    /// <summary>The entity with only the components and values that differ from its template; the same data when it names none.</summary>
    public EntityData Reduce(EntityData data)
    {
        if (data.Template is not { } template)
        {
            return data;
        }

        Dictionary<string, JsonElement> templateComponents = GetComponents(template);
        Dictionary<string, SceneComponent> registered = _spawner.Components.ToDictionary(static component => component.Name);
        EntityData reduced = new() { Name = data.Name, Template = template, Folder = data.Folder };
        foreach ((string name, JsonElement value) in data.Components)
        {
            if (!templateComponents.TryGetValue(name, out JsonElement under) || !registered.TryGetValue(name, out SceneComponent? component))
            {
                reduced.Components.Add(name, value);
                continue;
            }

            JsonElement normalUnder = component.Rewrite(under);
            JsonElement normalValue = component.Rewrite(value);
            if (JsonMerge.Diff(normalUnder, normalValue) is { } difference)
            {
                reduced.Components.Add(name, difference);
            }
        }

        return reduced;
    }

    /// <summary>Whether the entity's template, or a base of it, is the file at <paramref name="fullPath"/>.</summary>
    public bool Uses(EntityData data, string fullPath)
    {
        string? template = data.Template;
        HashSet<string> seen = [];
        while (template is not null && seen.Add(template))
        {
            string templatePath = _content.GetFullPath(template);
            if (templatePath == fullPath)
            {
                return true;
            }

            EntityTemplateFile file = _content.Load(template, RuntimeJsonContext.Default.EntityTemplateFile);
            template = file.Base;
        }

        return false;
    }

    /// <summary>The template's components, each laid over its bases' values.</summary>
    private Dictionary<string, JsonElement> GetComponents(string path)
    {
        List<EntityTemplateFile> chain = [];
        HashSet<string> seen = [];
        string? next = path;
        while (next is not null)
        {
            if (!seen.Add(next))
            {
                throw new InvalidDataException($"The entity template {next} is its own base.");
            }

            EntityTemplateFile file = _content.Load(next, RuntimeJsonContext.Default.EntityTemplateFile);
            chain.Add(file);
            next = file.Base;
        }

        Dictionary<string, JsonElement> components = [];
        for (int index = chain.Count - 1; index >= 0; index--)
        {
            foreach ((string name, JsonElement value) in chain[index].Components)
            {
                if (components.TryGetValue(name, out JsonElement under))
                {
                    components[name] = JsonMerge.Merge(under, value);
                }
                else
                {
                    components[name] = value;
                }
            }
        }

        return components;
    }
}
