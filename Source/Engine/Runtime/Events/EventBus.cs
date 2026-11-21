namespace FlickerVoid.Engine.Runtime.Events;

/// <summary>
/// Queues events, plain structs, that systems send, and delivers them to the handlers systems add when the game dispatches, after
/// each group of systems runs: in the order they were sent, each to its type's handlers in the order they were added. Events sent
/// while dispatching are delivered in the same dispatch.
/// </summary>
public sealed class EventBus
{
    /// <summary>More events than this in one dispatch means handlers keep sending each other events.</summary>
    public const int MaxEventsPerDispatch = 100_000;

    private readonly Dictionary<Type, Channel> _channels = [];
    private readonly Queue<Channel> _pending = new();

    public void Send<T>(in T value)
        where T : struct
    {
        Channel<T> channel = GetChannel<T>();
        channel.Enqueue(value);
        _pending.Enqueue(channel);
    }

    public void Handle<T>(GameEventHandler<T> handler)
        where T : struct
    {
        Channel<T> channel = GetChannel<T>();
        channel.Handlers.Add(handler);
    }

    /// <summary>Delivers every queued event, and every event their handlers send.</summary>
    public void Dispatch()
    {
        int delivered = 0;
        while (_pending.TryDequeue(out Channel? channel))
        {
            delivered++;
            if (delivered > MaxEventsPerDispatch)
            {
                _pending.Clear();
                throw new InvalidOperationException($"Handlers sent more than {MaxEventsPerDispatch} events in one dispatch; the last was {channel.Name}.");
            }

            channel.DeliverNext();
        }
    }

    private Channel<T> GetChannel<T>()
        where T : struct
    {
        if (_channels.TryGetValue(typeof(T), out Channel? existing))
        {
            return (Channel<T>)existing;
        }

        Channel<T> channel = new();
        _channels.Add(typeof(T), channel);
        return channel;
    }

    private abstract class Channel
    {
        public abstract string Name { get; }

        public abstract void DeliverNext();
    }

    private sealed class Channel<T> : Channel
        where T : struct
    {
        private readonly Queue<T> _queue = new();

        public List<GameEventHandler<T>> Handlers { get; } = [];

        public override string Name => typeof(T).Name;

        public void Enqueue(in T value) => _queue.Enqueue(value);

        public override void DeliverNext()
        {
            T value = _queue.Dequeue();
            foreach (GameEventHandler<T> handler in Handlers)
            {
                handler(in value);
            }
        }
    }
}
