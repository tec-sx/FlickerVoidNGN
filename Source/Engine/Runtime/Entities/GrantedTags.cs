using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Runtime.Entities;

/// <summary>Adds the tags one source grants (an outfit, a modifier, an active ability) to an entity's <see cref="OwnedTags"/>, keeping the tags others gave.</summary>
public static class GrantedTags
{
    /// <summary>Takes <paramref name="previous"/> off once each and adds <paramref name="granted"/>, so two sources granting a tag both have to let it go. Adding the component moves the entity, so update the source's own component first.</summary>
    public static void Replace(World world, Entity owner, IReadOnlyList<Tag> previous, IReadOnlyList<Tag> granted)
    {
        List<Tag> owned = [];
        if (world.TryGet(owner, out OwnedTags current))
        {
            owned.AddRange(current.Tags);
        }

        foreach (Tag applied in previous)
        {
            owned.Remove(applied);
        }

        owned.AddRange(granted);
        OwnedTags updated = new(owned);
        if (world.Has<OwnedTags>(owner))
        {
            world.Set(owner, updated);
        }
        else
        {
            world.Add(owner, updated);
        }
    }
}
