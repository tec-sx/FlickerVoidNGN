using Arch.Core;

using FlickerVoid.Engine.Interaction.Toggles;

namespace FlickerVoid.Engine.Creations;

/// <summary>Keeps which doors of a saveable creation are open, as parts are never saved themselves, and puts them back as a load finds them.</summary>
public sealed class PartToggleSaves(World world, CreationParts parts, JointSystem joints)
{
    /// <summary>The open state of each toggle part of the root, or null when it has none.</summary>
    public SavedPartToggles? Save(Entity root)
    {
        SavedPartToggles saved = new();
        foreach (Entity part in parts.FindAll(root))
        {
            if (world.TryGet(part, out Toggle toggle) && world.TryGet(part, out CreationPart mark))
            {
                saved.Open[mark.Id] = toggle.Open;
            }
        }

        if (saved.Open.Count == 0)
        {
            return null;
        }

        return saved;
    }

    /// <summary>Opens and shuts the root's doors as saved, at once; a part the creation no longer has is skipped.</summary>
    public void Restore(Entity root, SavedPartToggles saved)
    {
        foreach ((string id, bool open) in saved.Open)
        {
            Entity part = parts.Find(root, id);
            if (part == Entity.Null || !world.Has<Toggle>(part))
            {
                continue;
            }

            ref Toggle toggle = ref world.Get<Toggle>(part);
            toggle.Open = open;
            joints.Snap(part, open);
        }
    }
}
