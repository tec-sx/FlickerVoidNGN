using System.Text.Json;

using FlickerVoid.Engine.Rendering.Materials;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Editor.Assets.Materials;

/// <summary>
/// What a material keeps from the parents above it: each value it would inherit, from the nearest parent setting it, and the
/// master its chain ends at, whose parameters it sets after the scene shader's.
/// </summary>
public sealed record MaterialLineage(IReadOnlyDictionary<string, InheritedValue> Inherited, string? MasterPath, MasterFile? Master)
{
    /// <summary>The material's parameters: the scene shader's, then its master's own.</summary>
    public IReadOnlyList<ShaderParameter> GetParameters(IReadOnlyList<ShaderParameter> scene)
    {
        if (Master is null)
        {
            return scene;
        }

        return [.. scene, .. Master.Parameters];
    }

    /// <summary>The material's lineage, its parents loaded through the content, unsaved edits included; a chain that comes back to itself throws.</summary>
    public static MaterialLineage Of(MaterialFile material, IContentManager content)
    {
        Dictionary<string, InheritedValue> inherited = [];
        HashSet<string> visited = new(ContentManager.PathComparer);
        string? parentPath = material.Parent;
        while (parentPath is not null && !parentPath.EndsWith(MasterFile.Extension, StringComparison.OrdinalIgnoreCase))
        {
            if (!visited.Add(parentPath))
            {
                throw new InvalidDataException($"The material's parents come back to {parentPath}.");
            }

            MaterialFile parent = content.Load(parentPath, RenderingJsonContext.Default.MaterialFile);
            foreach ((string name, JsonElement value) in parent.Parameters)
            {
                InheritedValue from = new(value, parentPath);
                inherited.TryAdd(name, from);
            }

            parentPath = parent.Parent;
        }

        MasterFile? master = null;
        if (parentPath is not null)
        {
            master = content.Load(parentPath, RenderingJsonContext.Default.MasterFile);
        }

        return new MaterialLineage(inherited, parentPath, master);
    }
}
