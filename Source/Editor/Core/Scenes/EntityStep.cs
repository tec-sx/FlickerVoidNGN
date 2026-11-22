using Arch.Core;

using FlickerVoid.Editor.Core.Undo;

using EntityData = FlickerVoid.Engine.Runtime.Scenes.EntityData;

namespace FlickerVoid.Editor.Core.Scenes;

/// <summary>An entity added to the scene or destroyed, kept as the scene file data it was made from or captured before it went.</summary>
internal sealed class EntityStep(string name, EditorScene scene, EntityData data, bool added) : UndoStep(name)
{
    public override long Size
    {
        get
        {
            long size = 0;
            foreach (System.Text.Json.JsonElement component in data.Components.Values)
            {
                string text = component.GetRawText();
                size += text.Length * sizeof(char);
            }

            return size;
        }
    }

    public override void Undo()
    {
        if (added)
        {
            Remove();
        }
        else
        {
            scene.AddEntity(data);
        }
    }

    public override void Redo()
    {
        if (added)
        {
            scene.AddEntity(data);
        }
        else
        {
            Remove();
        }
    }

    private void Remove()
    {
        Entity? entity = scene.Find(data.Name);
        if (entity is not null)
        {
            scene.DestroyEntity(entity.Value);
        }
    }
}
