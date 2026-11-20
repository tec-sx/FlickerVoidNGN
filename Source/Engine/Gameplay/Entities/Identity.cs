using FlickerVoid.Engine.Gameplay.Definitions;

namespace FlickerVoid.Engine.Gameplay.Entities;

/// <summary>Component naming the character an entity is, so rules can read its definition's fragments.</summary>
public readonly record struct Identity(CharacterDefinition Definition);
