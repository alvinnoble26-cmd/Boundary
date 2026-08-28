using NUnit.Framework;
using UnityEngine;

public sealed class VoidAbilityTests
{
    [Test]
    public void UsesRequestedTimingAndRadius()
    {
        Assert.AreEqual(45f, VoidAbility.CooldownSeconds, 0.001f);
        Assert.AreEqual(15f, VoidAbility.DurationSeconds, 0.001f);
        Assert.AreEqual(18f, VoidAbility.GravityAcceleration, 0.001f);
        Assert.AreEqual(3f, VoidAbility.DarkTransitionSeconds, 0.001f);
        Assert.AreEqual(15f, VoidAbility.ImmunitySeconds, 0.001f);
        Assert.AreEqual(70f, VoidAbility.GravityRadius, 0.001f);
        Assert.AreEqual(10f, VoidAbility.BlackHoleSpawnDistance, 0.001f);
        Assert.AreEqual(0.5f, BoundaryArenaPresentation.ArenaAmbientLightMultiplier, 0.001f);
        Assert.AreEqual(6f, BoundaryArenaPresentation.VoidWallGlowIntensity, 0.001f);
    }

    [Test]
    public void OnlyActivatesWhenOpponentHasLessHealth()
    {
        Assert.IsTrue(VoidAbility.CanActivate(80f, 79f));
        Assert.IsFalse(VoidAbility.CanActivate(80f, 80f));
        Assert.IsFalse(VoidAbility.CanActivate(80f, 81f));
    }

    [Test]
    public void PracticeActivationDoesNotRequireOpponent()
    {
        Assert.IsTrue(VoidAbility.CanActivateForMode(true, false, 100f, 0f));
    }

    [Test]
    public void MultiplayerStillRequiresDamagedOpponent()
    {
        Assert.IsFalse(VoidAbility.CanActivateForMode(false, false, 100f, 0f));
        Assert.IsFalse(VoidAbility.CanActivateForMode(false, true, 60f, 60f));
        Assert.IsTrue(VoidAbility.CanActivateForMode(false, true, 61f, 60f));
    }

    [Test]
    public void EnemyHighlightIsRestrictedToVoidCasterView()
    {
        Assert.IsTrue(VoidAbility.ShouldShowEnemyHighlight(true, true));
        Assert.IsFalse(VoidAbility.ShouldShowEnemyHighlight(false, true));
        Assert.IsFalse(VoidAbility.ShouldShowEnemyHighlight(true, false));
    }

    [Test]
    public void AppliesRequestedSpeedModifiers()
    {
        Assert.AreEqual(1.3f, VoidAbility.CasterSpeedMultiplier, 0.001f);
        Assert.AreEqual(0.7f, VoidAbility.OpponentSpeedMultiplier, 0.001f);
    }

    [Test]
    public void GravityFallsToZeroAtSeventyUnits()
    {
        Assert.AreEqual(1f, VoidAbility.GravityFalloff(0f), 0.001f);
        Assert.AreEqual(0.5f, VoidAbility.GravityFalloff(35f), 0.001f);
        Assert.AreEqual(0f, VoidAbility.GravityFalloff(70f), 0.001f);
        Assert.AreEqual(0f, VoidAbility.GravityFalloff(90f), 0.001f);
    }

    [Test]
    public void GravityPullIsNoticeableButAllowsCounterMovement()
    {
        Vector3 velocityChange = VoidAbility.GravityVelocityChange(Vector3.forward * 35f, 0.1f);

        Assert.AreEqual(0.9f, velocityChange.magnitude, 0.001f);
        Assert.AreEqual(Vector3.zero,
            VoidAbility.GravityVelocityChange(Vector3.forward * 70f, 0.1f));
    }

    [Test]
    public void BlackHoleSpawnsTenUnitsAheadOnTheGroundPlane()
    {
        Vector3 position = VoidAbility.GetBlackHoleGroundPosition(new Vector3(3f, 2f, 4f),
            new Vector3(0f, 5f, 1f));

        Assert.AreEqual(new Vector3(3f, 2f, 14f), position);
    }
}
