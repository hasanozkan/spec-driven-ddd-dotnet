namespace Library.Contracts;

public interface IEventBus
{
    void Publish<TEvent>(TEvent message) where TEvent : notnull;

    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;
}

/// <summary>
/// Synchronous and in-process: the point is the shape — contexts publish facts
/// and subscribe to facts, never call each other. In production the same
/// Publish would write to a transactional outbox feeding a broker.
/// </summary>
public sealed class InProcessEventBus : IEventBus
{
    private readonly Dictionary<Type, List<Action<object>>> _handlers = [];

    public void Publish<TEvent>(TEvent message) where TEvent : notnull
    {
        if (_handlers.TryGetValue(typeof(TEvent), out var handlers))
        {
            foreach (var handler in handlers)
            {
                handler(message);
            }
        }
    }

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
        {
            handlers = [];
            _handlers[typeof(TEvent)] = handlers;
        }
        handlers.Add(e => handler((TEvent)e));
    }
}
