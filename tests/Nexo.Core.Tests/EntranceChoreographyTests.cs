using Nexo.Core.Shell;

namespace Nexo.Core.Tests;

public sealed class EntranceChoreographyTests
{
    [Fact]
    public void BlocksComeFirst_TopToBottom_AndEachContentAfterItsBlock()
    {
        var plan = EntranceChoreography.Plan(wordCount: 3, chipCount: 4, messageCount: 0);

        Assert.True(plan.Header < plan.CardBlock);
        Assert.True(plan.CardBlock < plan.ComposerBlock);
        Assert.True(plan.Mark > plan.CardBlock);
        Assert.True(plan.ComposerContent > plan.ComposerBlock);
    }

    [Fact]
    public void Greeting_ThenDescription_ThenChips_InOrder()
    {
        var plan = EntranceChoreography.Plan(wordCount: 3, chipCount: 4, messageCount: 0);

        Assert.Equal(3, plan.Words.Count);
        Assert.True(plan.Words[0] > plan.Mark);
        Assert.Equal(plan.Words.OrderBy(t => t), plan.Words);
        Assert.True(plan.Description > plan.Words[^1]);
        Assert.True(plan.Chips[0] > plan.Description);
        Assert.Equal(plan.Chips.OrderBy(t => t), plan.Chips);
    }

    [Fact]
    public void ManyPieces_SqueezeInsteadOfStretchingTheWholeEntrance()
    {
        var shortPlan = EntranceChoreography.Plan(wordCount: 3, chipCount: 4, messageCount: 0);
        var longPlan = EntranceChoreography.Plan(wordCount: 40, chipCount: 30, messageCount: 50);

        Assert.True(longPlan.Words[^1] - longPlan.Words[0] <= EntranceChoreography.MaximumSpread);
        Assert.True(longPlan.Chips[^1] - longPlan.Chips[0] <= EntranceChoreography.MaximumSpread);
        Assert.True(longPlan.Messages[^1] - longPlan.Messages[0] <= EntranceChoreography.MaximumSpread);
        Assert.True(longPlan.Settled <= TimeSpan.FromSeconds(2));
        Assert.True(shortPlan.Settled <= TimeSpan.FromMilliseconds(1600));
    }

    [Fact]
    public void NothingToShow_StillProducesAValidPlan()
    {
        var plan = EntranceChoreography.Plan(wordCount: 0, chipCount: 0, messageCount: 0);

        Assert.Empty(plan.Words);
        Assert.Empty(plan.Chips);
        Assert.Empty(plan.Messages);
        Assert.True(plan.Settled > plan.ComposerContent);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void PlaysFull_OnlyOnTheFirstOpenWithAnimations(bool first, bool animations, bool expected) =>
        Assert.Equal(expected, EntranceChoreography.PlaysFull(first, animations));
}
