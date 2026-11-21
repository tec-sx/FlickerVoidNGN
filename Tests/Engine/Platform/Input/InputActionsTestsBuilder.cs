using System.Text.Json.Nodes;

using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Runtime.Events;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Platform.Tests.Input;

/// <summary>Builds input actions over a fake keyboard and mouse with a map written as JSON, read as a game's data asset, recording the events they send.</summary>
internal sealed class InputActionsTestsBuilder : IDisposable
{
    private readonly Game _game = new(NullLoggerFactory.Instance);

    public InputActionsTestsBuilder()
    {
        _game.AddInput();
        _game.Events.Handle((in InputActionEvent sent) => Events.Add(sent));
    }

    public InputState Input { get; } = new();

    public EventBus Bus => _game.Events;

    public List<InputActionEvent> Events { get; } = [];

    public InputActions Build(string json)
    {
        JsonObject file = JsonNode.Parse(json)!.AsObject();
        InputMap map = (InputMap)_game.Data.ReadAsset("Input.fvdata", file);
        return new InputActions(Input, _game.Events, map);
    }

    /// <summary>Runs one frame: the actions read the input, the events are dispatched and the input ends its frame.</summary>
    public void Frame(InputActions actions)
    {
        GameTime time = new(TimeSpan.FromSeconds(1.0 / 60), TimeSpan.Zero, 1);
        actions.Update(in time);
        _game.Events.Dispatch();
    }

    public void Dispose()
    {
        _game.Dispose();
    }
}
