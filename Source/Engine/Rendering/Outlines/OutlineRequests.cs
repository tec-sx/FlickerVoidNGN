using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Rendering.Outlines;

/// <summary>
/// Outlines several sources want at once, like granted tags: each source requests a colour with a priority and withdraws it, and the
/// entity's <see cref="Outline"/> is the highest priority's (the latest of equals), or the one it had before any request.
/// </summary>
public static class OutlineRequests
{
    /// <summary>Outlines the entity for <paramref name="source"/>, replacing that source's earlier request.</summary>
    public static void Request(World world, Entity entity, Tag source, Vector3 color, int priority = 0)
    {
        if (!world.IsAlive(entity))
        {
            return;
        }

        if (!world.TryGet(entity, out RequestedOutlines requests))
        {
            requests = new RequestedOutlines();
            if (world.TryGet(entity, out Outline authored))
            {
                requests.Authored = authored;
            }

            world.Add(entity, requests);
        }

        requests.Requests.RemoveAll(request => request.Source == source);
        OutlineRequest made = new(source, color, priority);
        requests.Requests.Add(made);
        Apply(world, entity, requests);
    }

    /// <summary>Takes back <paramref name="source"/>'s request; the next request shows, or the outline the entity had, or none.</summary>
    public static void Withdraw(World world, Entity entity, Tag source)
    {
        if (!world.IsAlive(entity) || !world.TryGet(entity, out RequestedOutlines requests))
        {
            return;
        }

        if (requests.Requests.RemoveAll(request => request.Source == source) > 0)
        {
            Apply(world, entity, requests);
        }
    }

    public static bool IsRequested(World world, Entity entity, Tag source)
    {
        return world.IsAlive(entity) && world.TryGet(entity, out RequestedOutlines requests) && requests.Requests.Exists(request => request.Source == source);
    }

    private static void Apply(World world, Entity entity, RequestedOutlines requests)
    {
        if (requests.Requests.Count == 0)
        {
            world.Remove<RequestedOutlines>(entity);
            if (requests.Authored is { } authored)
            {
                Set(world, entity, authored);
            }
            else if (world.Has<Outline>(entity))
            {
                world.Remove<Outline>(entity);
            }

            return;
        }

        OutlineRequest top = requests.Requests[0];
        foreach (OutlineRequest request in requests.Requests)
        {
            if (request.Priority >= top.Priority)
            {
                top = request;
            }
        }

        Outline outline = new() { Color = top.Color };
        Set(world, entity, outline);
    }

    private static void Set(World world, Entity entity, Outline outline)
    {
        if (world.Has<Outline>(entity))
        {
            world.Set(entity, outline);
        }
        else
        {
            world.Add(entity, outline);
        }
    }
}
