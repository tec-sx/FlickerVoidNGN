using FlickerVoid.Engine.Runtime.Events;

namespace FlickerVoid.Engine.Runtime.Tests.Events;

public sealed class EventBusTests
{
    private readonly EventBusTestsBuilder _builder = new();

    [Fact]
    public void DeliversEventsInTheOrderTheyWereSent()
    {
        EventBus events = _builder.WithRecordingHandlers().Build();
        events.Send(new Opened(1));
        events.Send(new Closed(2));
        events.Send(new Opened(3));

        events.Dispatch();

        _builder.Delivered.Should().Equal("opened 1", "closed 2", "opened 3");
    }

    [Fact]
    public void HoldsEventsUntilDispatched()
    {
        EventBus events = _builder.WithRecordingHandlers().Build();

        events.Send(new Opened(1));

        _builder.Delivered.Should().BeEmpty();
    }

    [Fact]
    public void DeliversEventsSentWhileDispatchingInTheSameDispatch()
    {
        EventBus events = _builder.WithRecordingHandlers().Build();
        events.Handle((in Opened opened) => events.Send(new Closed(opened.Id)));
        events.Send(new Opened(1));

        events.Dispatch();

        _builder.Delivered.Should().Equal("opened 1", "closed 1");
    }

    [Fact]
    public void DeliversToEveryHandlerInTheOrderTheyWereAdded()
    {
        EventBus events = _builder.Build();
        List<string> calls = [];
        events.Handle((in Opened _) => calls.Add("first"));
        events.Handle((in Opened _) => calls.Add("second"));
        events.Send(new Opened(1));

        events.Dispatch();

        calls.Should().Equal("first", "second");
    }

    [Fact]
    public void DropsEventsNothingHandles()
    {
        EventBus events = _builder.Build();
        events.Send(new Opened(1));

        Action dispatch = events.Dispatch;

        dispatch.Should().NotThrow();
    }

    [Fact]
    public void StopsAnEndlessChainOfEvents()
    {
        EventBus events = _builder.Build();
        events.Handle((in Opened opened) => events.Send(opened));
        events.Send(new Opened(1));

        Action dispatch = events.Dispatch;

        dispatch.Should().Throw<InvalidOperationException>().WithMessage("*Opened*");
    }
}
