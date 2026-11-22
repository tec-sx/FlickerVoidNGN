using System.Collections.ObjectModel;
using System.Globalization;

using FlickerVoid.Engine.Abilities.Activation;
using FlickerVoid.Engine.Attributes.Modifiers;
using FlickerVoid.Engine.Attributes.Values;
using FlickerVoid.Engine.Gameplay.Tags;
using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Interface.Hub.Character;

/// <summary>The hub's character page in three sections: the protagonist's attributes, what changes them now, and its abilities.</summary>
public sealed class CharacterPageViewModel : HubPageViewModel
{
    private readonly AttributeSystem _attributes;
    private readonly AbilitySystem _abilities;

    public CharacterPageViewModel(Game game)
        : base("Character")
    {
        _attributes = game.Services.GetService<AttributeSystem>()!;
        _abilities = game.Services.GetService<AbilitySystem>()!;
        game.Events.Handle((in AttributeChangedEvent e) => RefreshFor(e.Owner));
        game.Events.Handle((in AbilityActivatedEvent e) => RefreshFor(e.Owner));
        game.Events.Handle((in AbilityEndedEvent e) => RefreshFor(e.Owner));
    }

    /// <summary>Each attribute with its current value, and its base in brackets when something changes it.</summary>
    public ObservableCollection<StatRowViewModel> Attributes { get; } = [];

    public ObservableCollection<string> Modifiers { get; } = [];

    public ObservableCollection<string> Abilities { get; } = [];

    protected override void Refresh()
    {
        Attributes.Clear();
        IReadOnlyDictionary<Tag, AttributeValue> values = _attributes.GetValues(Owner);
        foreach ((Tag tag, AttributeValue value) in values)
        {
            string name = DisplayNames.Leaf(tag);
            if (value.Definition is not null)
            {
                name = DisplayNames.Of(value.Definition);
            }

            string text = value.Current.ToString("0.#", CultureInfo.InvariantCulture);
            if (value.Current != value.Base)
            {
                string baseText = value.Base.ToString("0.#", CultureInfo.InvariantCulture);
                text = $"{text} ({baseText})";
            }

            Attributes.Add(new StatRowViewModel(name, text));
        }

        Modifiers.Clear();
        foreach (AppliedModifier applied in _attributes.GetModifiers(Owner))
        {
            Modifiers.Add(DisplayNames.Of(applied.Modifier));
        }

        Abilities.Clear();
        foreach (AbilitySpec spec in _abilities.GetAbilities(Owner))
        {
            Abilities.Add(DisplayNames.Of(spec.Ability));
        }
    }
}
