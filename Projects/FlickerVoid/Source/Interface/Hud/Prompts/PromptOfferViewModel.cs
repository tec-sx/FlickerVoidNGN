namespace FlickerVoid.Interface.Hud.Prompts;

/// <summary>An offer the prompt shows: its name, and why it can't be done when its conditions show that.</summary>
public sealed record PromptOfferViewModel(string Name, bool IsAvailable, string LockedReason)
{
    public bool IsLocked => LockedReason.Length > 0;
}
