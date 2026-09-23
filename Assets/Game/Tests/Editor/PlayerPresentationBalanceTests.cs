using NUnit.Framework;
using UnityEngine;

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
    public void BlackHoleGrowthDoublesRateAndStopsAtOneAndAHalfTimesOldMaximum()
    {
        Assert.AreEqual(Vector3.one * 10f,
            Grow.ResolveScale(Vector3.one * 2f, 8f, 1f, 31.5f));
        Assert.AreEqual(Vector3.one * 31.5f,
            Grow.ResolveScale(Vector3.one * 30f, 8f, 1f, 31.5f));
    }

    [Test]
    public void RequestedMovementAndKnifeSpeedMultipliersAreApplied()
    {
        Assert.AreEqual(570f, BullseyeAbility.ProjectileSpeed, 0.001f);
        Assert.AreEqual(10.92f, PlayerMovement.DefaultMaxSpeed, 0.001f);
    }

    [Test]
    public void BullseyeTargetUsesOpponentBodyForItsInnerMarker()
    {
        Assert.AreEqual(1.74f * 1.3f, BullseyeTargetPresentation.OuterRingRadius, 0.001f);
        Assert.IsTrue(BullseyeTargetPresentation.InnerTargetUsesOpponentBody);
        Assert.Greater(PlayerOutlinePresentation.BullseyeWidth,
            PlayerOutlinePresentation.NormalWidth);
        Assert.Greater(PlayerOutlinePresentation.VoidWidth,
            PlayerOutlinePresentation.BullseyeWidth);
    }

    [Test]
    public void JumpCueIsQuieterAndAbilityAudioHasFiniteRange()
    {
        Assert.Less(SfxManager.JumpVolume, 0.5f);
        Assert.Greater(SfxManager.AbilitySoundMaxDistance, 10f);
        Assert.LessOrEqual(SfxManager.AbilitySoundMaxDistance, 60f);
    }
}
