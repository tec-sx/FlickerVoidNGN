using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Platform.Input;

/// <summary>
/// What the player may do while the game is in one situation, such as a conversation, a menu or a cutscene: the actions read while it
/// is the innermost context <see cref="InputActions"/> holds; every other action reads as up.
/// </summary>
public sealed class InputContext
{
    /// <summary>Names the context for the code that enters it, such as <c>Input.Context.Conversation</c>.</summary>
    public Tag Id { get; set; }

    public List<Tag> Actions { get; set; } = [];
}
