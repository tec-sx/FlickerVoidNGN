namespace FlickerVoid.Engine.Interaction.Tests.Interactors;

public sealed class InteractorTagsConditionTests
{
    [Fact]
    public void HoldsWhenTheInteractorHasAnyRequiredTag()
    {
        bool held = new InteractorTagsConditionTestsBuilder().Requiring(false, "Skill.Lockpick", "Skill.Strength").WithOwned("Skill.Strength").Check();

        held.Should().BeTrue();
    }

    [Fact]
    public void NeedsEveryRequiredTagWithRequireAll()
    {
        bool held = new InteractorTagsConditionTestsBuilder().Requiring(true, "Skill.Lockpick", "Skill.Strength").WithOwned("Skill.Strength").Check();

        held.Should().BeFalse();
    }

    [Fact]
    public void FailsOnABlockedTag()
    {
        bool held = new InteractorTagsConditionTestsBuilder().Blocking("Status.Hands.Full").WithOwned("Status.Hands.Full.Carrying").Check();

        held.Should().BeFalse();
    }

    [Fact]
    public void AnInteractorWithoutTagsPassesOnlyWhenNothingIsRequired()
    {
        bool free = new InteractorTagsConditionTestsBuilder().Blocking("Status.Hands.Full").Check();
        bool required = new InteractorTagsConditionTestsBuilder().Requiring(false, "Skill.Lockpick").Check();

        free.Should().BeTrue();
        required.Should().BeFalse();
    }
}
