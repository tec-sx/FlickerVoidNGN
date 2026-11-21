using FlickerVoid.Engine.Runtime.Events;

namespace FlickerVoid.Engine.Runtime.Tests.Events;

internal readonly record struct Opened(int Id);

internal readonly record struct Closed(int Id);

/// <summary>Builds the event bus under test, optionally with handlers that record what they receive.</summary>
internal sealed class EventBusTestsBuilder
{
    private bool _recording;

    public List<string> Delivered { get; } = [];

    public EventBusTestsBuilder WithRecordingHandlers()
    {
        _recording = true;
        return this;
    }

    public EventBus Build()
    {
        EventBus events = new();
        if (_recording)
        {
            events.Handle((in Opened opened) => Delivered.Add($"opened {opened.Id}"));
            events.Handle((in Closed closed) => Delivered.Add($"closed {closed.Id}"));
        }

        return events;
    }
}
