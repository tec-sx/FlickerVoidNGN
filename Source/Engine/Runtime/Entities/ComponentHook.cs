using Arch.Core;

namespace FlickerVoid.Engine.Runtime.Entities;

/// <summary>Runs when an entity with the component enters or leaves a lifecycle phase; it may change the component.</summary>
public delegate void ComponentHook<T>(Entity entity, ref T component)
    where T : struct;
