using Arch.Core;

namespace FlickerVoid.Editor.Core.Selection;

/// <summary>The entity the editor works on, if any; raises <see cref="Changed"/> when another is selected.</summary>
public sealed class EditorSelection
{
    public Entity? Selected { get; private set; }

    public event Action? Changed;

    public void Select(Entity? entity)
    {
        if (Selected == entity)
        {
            return;
        }

        Selected = entity;
        Changed?.Invoke();
    }
}
