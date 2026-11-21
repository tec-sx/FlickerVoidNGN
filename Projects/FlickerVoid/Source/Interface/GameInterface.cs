using Arch.Core;
using Arch.System;

using Avalonia.Controls;

using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Runtime.Loop;
using FlickerVoid.Gameplay.Protagonist.Control;
using FlickerVoid.Interface.Hub;
using FlickerVoid.Interface.Hud;

namespace FlickerVoid.Interface;

/// <summary>
/// FlickerVoid's interface over a game: the HUD, and the hub over it while open, with a frame system that opens and closes the hub
/// (Hub toggles it, Back closes it, Next and Previous Page switch pages), shows the prompt for what the pointer is over and turns the
/// minimap. Draw <see cref="View"/> with the engine's <c>GameUi</c>.
/// </summary>
public sealed class GameInterface : ISystem<GameTime>
{
    public static readonly Tag HubInput = new("Input.Hub");
    public static readonly Tag BackInput = new("Input.Back");
    public static readonly Tag NextPageInput = new("Input.NextPage");
    public static readonly Tag PreviousPageInput = new("Input.PreviousPage");

    private readonly PointerPicker? _picker;
    private readonly InputActions? _actions;
    private readonly HudView _hudView;

    public GameInterface(Game game)
    {
        _picker = game.Services.GetService<PointerPicker>();
        _actions = game.Services.GetService<InputActions>();
        Hud = new HudViewModel(game);
        Hub = new HubViewModel(game);
        _hudView = new HudView { DataContext = Hud };
        HubView hubView = new() { DataContext = Hub };
        View = new Panel { Children = { _hudView, hubView } };
    }

    public HudViewModel Hud { get; }

    public HubViewModel Hub { get; }

    public Control View { get; }

    public void Initialize()
    {
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        ReadHubInput();
        _hudView.IsVisible = !Hub.IsOpen;
        Entity hovered = Entity.Null;
        if (_picker is not null && _picker.TryPick(out RayHit hit))
        {
            hovered = hit.Entity;
        }

        Hud.Prompt.Update(hovered);
        Hud.Minimap.Update();
    }

    private void ReadHubInput()
    {
        if (_actions is null)
        {
            return;
        }

        if (_actions.WasPressed(HubInput))
        {
            Hub.Toggle();
        }
        else if (Hub.IsOpen && _actions.WasPressed(BackInput))
        {
            Hub.Close();
        }
        else if (Hub.IsOpen && _actions.WasPressed(NextPageInput))
        {
            Hub.NextPageCommand.Execute(null);
        }
        else if (Hub.IsOpen && _actions.WasPressed(PreviousPageInput))
        {
            Hub.PreviousPageCommand.Execute(null);
        }
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
    }
}
