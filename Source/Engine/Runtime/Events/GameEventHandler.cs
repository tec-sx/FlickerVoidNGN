namespace FlickerVoid.Engine.Runtime.Events;

/// <summary>Handles one event delivered by an <see cref="EventBus"/>.</summary>
public delegate void GameEventHandler<T>(in T value)
    where T : struct;
