namespace FlickerVoid.Interface.Hub.Character;

/// <summary>A named value on the character page, such as an attribute with its current value.</summary>
public sealed record StatRowViewModel(string Name, string Value);
