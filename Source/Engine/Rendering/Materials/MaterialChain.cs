using System.Text.Json;

using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// Flattens a material instance and the parents above it into one material: each value from the nearest material in the chain
/// that sets it, as Unreal's material instances inherit their parents' parameters.
/// </summary>
public static class MaterialChain
{
    /// <summary>The material with its parents' values where it sets none; <paramref name="loadParent"/> loads a parent by its content path.</summary>
    public static MaterialFile Flatten(MaterialFile material, Func<string, MaterialFile> loadParent)
    {
        if (material.Parent is null)
        {
            return material;
        }

        Dictionary<string, JsonElement> parameters = new(material.Parameters);
        List<string> chain = [];
        HashSet<string> visited = new(ContentManager.PathComparer);
        string? parentPath = material.Parent;
        while (parentPath is not null)
        {
            chain.Add(parentPath);
            if (!visited.Add(parentPath))
            {
                string loop = string.Join(" -> ", chain);
                throw new InvalidDataException($"The material's parents come back to themselves: {loop}.");
            }

            MaterialFile parent = loadParent(parentPath);
            foreach ((string name, JsonElement value) in parent.Parameters)
            {
                parameters.TryAdd(name, value);
            }

            parentPath = parent.Parent;
        }

        return new MaterialFile { Schema = material.Schema, Parameters = parameters };
    }
}
