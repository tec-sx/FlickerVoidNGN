using System.Text.Json;

using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Materials;

/// <summary>
/// Flattens a material instance and the parents above it into one material: each value from the nearest material in the chain
/// that sets it, as Unreal's material instances inherit their parents' parameters. A chain ends at a master
/// (<see cref="MasterFile"/>), which the flattened material keeps as its parent, or at a material without a parent.
/// </summary>
public static class MaterialChain
{
    /// <summary>
    /// The material with its parents' values where it sets none and the chain's master, if any, as its parent;
    /// <paramref name="loadParent"/> loads a parent material by its content path.
    /// </summary>
    public static MaterialFile Flatten(MaterialFile material, Func<string, MaterialFile> loadParent)
    {
        if (material.Parent is null || IsMaster(material.Parent))
        {
            return material;
        }

        Dictionary<string, JsonElement> parameters = new(material.Parameters);
        List<string> chain = [];
        HashSet<string> visited = new(ContentManager.PathComparer);
        string? parentPath = material.Parent;
        while (parentPath is not null && !IsMaster(parentPath))
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

        return new MaterialFile { Schema = material.Schema, Parent = parentPath, Parameters = parameters };
    }

    private static bool IsMaster(string path)
    {
        return path.EndsWith(MasterFile.Extension, StringComparison.OrdinalIgnoreCase);
    }
}
