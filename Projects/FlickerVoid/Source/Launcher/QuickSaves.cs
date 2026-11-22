using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Saves;

namespace FlickerVoid.Launcher;

/// <summary>Saves the game to the quick slot on <see cref="QuickSave"/> (F5) and loads it back on <see cref="QuickLoad"/> (F9).</summary>
public sealed class QuickSaves
{
    public const string Slot = "Quick";

    public static readonly Tag QuickSave = new("Input.QuickSave");

    public static readonly Tag QuickLoad = new("Input.QuickLoad");

    private readonly SaveSystem _saves;

    public QuickSaves(EventBus events, SaveSystem saves)
    {
        _saves = saves;
        events.Handle((in InputActionEvent action) => Handle(action));
    }

    private void Handle(in InputActionEvent action)
    {
        if (action.Phase != InputPhase.Pressed)
        {
            return;
        }

        if (action.Action == QuickSave)
        {
            _saves.Save(Slot);
        }
        else if (action.Action == QuickLoad)
        {
            _saves.Load(Slot);
        }
    }
}
