using System.Numerics;

using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Rendering.Outlines;

/// <summary>One source's wish to outline an entity in a colour, over the wishes of lower priority.</summary>
public readonly record struct OutlineRequest(Tag Source, Vector3 Color, int Priority);
