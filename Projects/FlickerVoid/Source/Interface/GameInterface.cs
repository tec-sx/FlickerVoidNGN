using Arch.Core;
using Arch.System;

using Avalonia.Controls;

using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Gameplay.Protagonist.Control;
using FlickerVoid.Interface.Hud;

namespace FlickerVoid.Interface;

/// <summary>
/// FlickerVoid's interface over a game: the HUD's view model and view, and a frame system that shows the prompt for what the
/// pointer is over. Draw <see cref="View"/> with the engine's <c>GameUi</c>.
/// </summary>
public sealed class GameInterface : ISystem<GameTime>
{
    private readonly PointerPicker? _picker;

    public GameInterface(Game game)
    {
        _picker = game.Services.GetService<PointerPicker>();
        Hud = new HudViewModel(game);
        View = new HudView { DataContext = Hud };
    }

    public HudViewModel Hud { get; }

    public Control View { get; }

    public void Initialize()
    {
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        Entity hovered = Entity.Null;
        if (_picker is not null && _picker.TryPick(out RayHit hit))
        {
            hovered = hit.Entity;
        }

        Hud.Prompt.Update(hovered);
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
    }
}
