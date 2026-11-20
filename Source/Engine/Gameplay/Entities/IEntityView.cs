using System.Diagnostics.CodeAnalysis;

namespace FlickerVoid.Engine.Gameplay.Entities;

/// <summary>Read access to an entity's components, so rules can look at entities without depending on the entity library.</summary>
public interface IEntityView
{
    /// <summary>The entity's component of type <typeparamref name="T"/>; false when it has none or no longer exists.</summary>
    bool TryGet<T>([MaybeNullWhen(false)] out T component);
}
