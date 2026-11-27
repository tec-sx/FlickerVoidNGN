using FlickerVoid.Engine.Audio.Mixing;
using FlickerVoid.Engine.Runtime.Debugging;

namespace FlickerVoid.Engine.Audio.Sounds;

/// <summary>The volumes as console variables (<c>audio.</c>), which the settings file sets and the audio system follows.</summary>
public sealed class AudioSettings
{
    public AudioSettings()
    {
        Variables = [Master, Music, Ambience, Effects, Dialogue, Interface];
        foreach (ConsoleVariable<float> variable in Variables)
        {
            variable.Changed += HandleChanged;
        }
    }

    /// <summary>Raised when any of the variables is set.</summary>
    public event Action? Changed;

    public ConsoleVariable<float> Master { get; } = ConsoleVariable.Create("audio.Master", "The volume of everything, from 0 to 1.", 1f);

    public ConsoleVariable<float> Music { get; } = ConsoleVariable.Create("audio.Music", "The volume of the music, from 0 to 1.", 1f);

    public ConsoleVariable<float> Ambience { get; } = ConsoleVariable.Create("audio.Ambience", "The volume of the ambience, from 0 to 1.", 1f);

    public ConsoleVariable<float> Effects { get; } = ConsoleVariable.Create("audio.Effects", "The volume of the world's sounds, from 0 to 1.", 1f);

    public ConsoleVariable<float> Dialogue { get; } = ConsoleVariable.Create("audio.Dialogue", "The volume of voices, from 0 to 1.", 1f);

    public ConsoleVariable<float> Interface { get; } = ConsoleVariable.Create("audio.Interface", "The volume of the interface's sounds, from 0 to 1.", 1f);

    public IReadOnlyList<ConsoleVariable<float>> Variables { get; }

    /// <summary>The volume of a bus, held between 0 and 1.</summary>
    public float GetVolume(AudioBus bus)
    {
        float volume = Variables[(int)bus].Value;
        return Math.Clamp(volume, 0, 1);
    }

    public void Register(IDebugConsole console)
    {
        foreach (ConsoleVariable<float> variable in Variables)
        {
            console.Add(variable);
        }
    }

    private void HandleChanged(float value)
    {
        Changed?.Invoke();
    }
}
