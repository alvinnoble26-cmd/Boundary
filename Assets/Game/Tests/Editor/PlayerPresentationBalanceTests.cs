using NUnit.Framework;

public sealed class PlayerPresentationBalanceTests
{
    [Test]
    public void EveryPlayerUsesRequestedWorldScale()
    {
        Assert.AreEqual(1.3f, PlayerMovement.CharacterScale, 0.001f);
        Assert.AreEqual(1.3f, PlayerMovement.ScaleDistance(1f), 0.001f);
    }

    [Test]
    public void BlackHoleThrowForceIsBoostedByHalf()
    {
        Assert.AreEqual(30f,
            BlackThrow.EffectiveThrowForce(BlackThrow.DefaultThrowForce), 0.001f);
    }

    [Test]
    public void RequestedMovementAndKnifeSpeedMultipliersAreApplied()
    {
        Assert.AreEqual(285f, BullseyeAbility.ProjectileSpeed, 0.001f);
        Assert.AreEqual(10.92f, PlayerMovement.DefaultMaxSpeed, 0.001f);
    }

    [Test]
    public void BullseyeTargetUsesOpponentBodyForItsInnerMarker()
    {
        Assert.AreEqual(1.74f * 1.3f, BullseyeTargetPresentation.OuterRingRadius, 0.001f);
        Assert.IsTrue(BullseyeTargetPresentation.InnerTargetUsesOpponentBody);
    }

    [Test]
    public void JumpCueIsQuieterAndAbilityAudioHasFiniteRange()
    {
        Assert.Less(SfxManager.JumpVolume, 0.5f);
        Assert.Greater(SfxManager.AbilitySoundMaxDistance, 10f);
        Assert.Less(SfxManager.AbilitySoundMaxDistance, 60f);
    }
}
