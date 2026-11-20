using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Arch.Core;

namespace FlickerVoid.Engine.Runtime.Scenes;

/// <summary>
/// A component type that scene files may add under <see cref="Name"/>, with the source-generated metadata that reads it,
/// so tools can read and change it on any entity without knowing its type.
/// </summary>
public abstract class SceneComponent
{
    private protected SceneComponent(string name, JsonTypeInfo typeInfo)
    {
        Name = name;
        TypeInfo = typeInfo;
    }

    public string Name { get; }

    /// <summary>The component's properties, with getters and setters that work on the boxed value <see cref="Get"/> returns.</summary>
    public JsonTypeInfo TypeInfo { get; }

    public abstract bool Has(World world, Entity entity);

    /// <summary>A boxed copy of the entity's component.</summary>
    public abstract object Get(World world, Entity entity);

    /// <summary>Gives the entity the component, replacing the one it has.</summary>
    public abstract void Set(World world, Entity entity, object component);

    /// <summary>Reads the component from JSON and gives it to the entity.</summary>
    public abstract void Read(World world, Entity entity, JsonElement data);

    public abstract void Remove(World world, Entity entity);

    /// <summary>The entity's component as JSON, every property written.</summary>
    public abstract JsonElement Write(World world, Entity entity);

    /// <summary>Reads the component from JSON and writes it back, every property written, to compare it with <see cref="Write"/>.</summary>
    public abstract JsonElement Rewrite(JsonElement data);
}
