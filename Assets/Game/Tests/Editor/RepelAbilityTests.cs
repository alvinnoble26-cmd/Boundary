using NUnit.Framework;

public sealed class RepelAbilityTests
{
    [Test]
    public void UsesHalfSecondBuildUp()
    {
        Assert.AreEqual(0.5f, RepelThrow.BuildUpSeconds, 0.001f);
    }

    [Test]
    public void EffectRadiusIsReducedByFortyPercent()
    {
        Assert.AreEqual(0.6f, RepelThrow.EffectRadiusMultiplier, 0.001f);
        Assert.AreEqual(132f, RepelThrow.EffectRadius, 0.001f);
    }
}
