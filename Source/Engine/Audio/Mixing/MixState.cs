using System.ComponentModel.DataAnnotations;

namespace FlickerVoid.Engine.Audio.Mixing;

/// <summary>
/// A situation the mix changes for: while it holds, its ducks lower and dull buses, fading in and out over <see cref="Fade"/>.
/// <c>Indoors</c> and <c>Paused</c> hold by themselves (the listener in an indoor volume, the game paused); the game turns the
/// others on and off by name (<c>Talking</c> while a conversation is on, <c>Hub open</c> while the hub is).
/// </summary>
public sealed class MixState
{
    public string Name { get; set; } = string.Empty;

    public List<MixDuck> Ducks { get; set; } = [];

    /// <summary>Seconds it takes to come in and to go out.</summary>
    [Range(0, 10)]
    public float Fade { get; set; } = 0.4f;
}
