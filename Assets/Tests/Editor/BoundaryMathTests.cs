#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class BoundaryMathTests
{
    [Test]
    public void TransitionRadius_IsSmoothAndHitsBothEndpoints()
    {
        Assert.That(BoundaryMath.TransitionRadius(106f, 68f, 0f, 7f), Is.EqualTo(106f).Within(0.001f));
        Assert.That(BoundaryMath.TransitionRadius(106f, 68f, 7f, 7f), Is.EqualTo(68f).Within(0.001f));
        Assert.That(BoundaryMath.TransitionRadius(106f, 68f, 3.5f, 7f), Is.EqualTo(87f).Within(0.001f));
    }

    [Test]
    public void StableGrounding_MeaningfullyReducesPull()
    {
        Vector3 player = Vector3.zero;
        Vector3 singularity = new Vector3(0f, 28f, 0f);
        Vector3 center = Vector3.zero;
        Vector3 airborne = BoundaryMath.PlayerPullAcceleration(
            player, singularity, center, -1f, 106f, 5.5f, false);
        Vector3 grounded = BoundaryMath.PlayerPullAcceleration(
            player, singularity, center, -1f, 106f, 5.5f, true);

        Assert.That(grounded.magnitude, Is.LessThan(airborne.magnitude * 0.2f));
    }

    [Test]
    public void ClosedRingPressure_PushesOutsidePlayerInwardAndUpward()
    {
        Vector3 acceleration = BoundaryMath.PlayerPullAcceleration(
            new Vector3(16f, 0f, 0f),
            new Vector3(0f, 28f, 0f),
            Vector3.zero,
            -1f,
            10f,
            12f,
            true);

        Assert.That(acceleration.x, Is.LessThan(-5f));
        Assert.That(acceleration.y, Is.GreaterThan(7f));
    }

    [Test]
    public void GravityPulse_HasWarningPeakAndRecovery()
    {
        Assert.That(BoundaryMath.RhythmicPulse(0.5f, 4f, 1f, 1f), Is.Zero);
        Assert.That(BoundaryMath.RhythmicPulse(1.5f, 4f, 1f, 1f), Is.EqualTo(1f).Within(0.001f));
        Assert.That(BoundaryMath.RhythmicPulse(2.5f, 4f, 1f, 1f), Is.Zero);
    }

    [Test]
    public void PlatformVoid_HasAnImmediateKillPlaneWithoutARescueCurrent()
    {
        Vector3 acceleration = BoundaryMath.PlayerPullAcceleration(
            new Vector3(22f, -8f, 0f),
            new Vector3(0f, 32f, 0f),
            Vector3.zero,
            -0.9f,
            68f,
            2.1f,
            false);

        Assert.That(BoundaryMath.IsBelowVoidKillPlane(-5f, -0.9f, 4f), Is.True);
        Assert.That(BoundaryMath.IsBelowVoidKillPlane(-4f, -0.9f, 4f), Is.False);
        Assert.That(acceleration.y, Is.LessThan(10f));
    }

    [Test]
    public void StableHash_IsRepeatableAndVariesByIndex()
    {
        int first = BoundaryMath.StableHash(90210, 4);
        Assert.That(BoundaryMath.StableHash(90210, 4), Is.EqualTo(first));
        Assert.That(BoundaryMath.StableHash(90210, 5), Is.Not.EqualTo(first));
    }

    [TestCase(BoundaryDisaster.BlackRain, "BLACK RAIN")]
    [TestCase(BoundaryDisaster.FalseSingularities, "FALSE SINGULARITIES")]
    [TestCase(BoundaryDisaster.UnstableMass, "UNSTABLE MASS")]
    public void EveryDisasterHasReadablePresentation(BoundaryDisaster disaster, string expectedName)
    {
        Assert.That(BoundaryMath.DisasterName(disaster), Is.EqualTo(expectedName));
        Assert.That(BoundaryMath.DisasterHint(disaster), Is.Not.Empty);
    }

    [Test]
    public void ReverseCurrent_IsNotInTheDisasterPool()
    {
        CollectionAssert.DoesNotContain(System.Enum.GetNames(typeof(BoundaryDisaster)), "ReverseCurrent");
        Assert.That(System.Enum.GetValues(typeof(BoundaryDisaster)).Length - 1, Is.EqualTo(9));
    }

    [Test]
    public void ArenaMassPopulation_LeavesExactlyOneQuarterForInnerRing()
    {
        Assert.That(BoundaryMatchController.ArenaMassPopulation, Is.EqualTo(20));
        Assert.That(BoundaryMatchController.ArenaMassInnerSurvivors,
            Is.EqualTo(BoundaryMatchController.ArenaMassPopulation / 4));
    }

    [Test]
    public void ArenaMasses_AreLargeAndAbilityPulseIsDecisive()
    {
        Assert.That(BoundaryMatchController.ArenaMassCubeScale, Is.GreaterThanOrEqualTo(2.8f));
        Assert.That(BoundaryMatchController.ArenaMassBlackHoleScale, Is.GreaterThanOrEqualTo(1.75f));
        Assert.That(BoundaryMath.ArenaMassAbilityVelocityChange(0f), Is.EqualTo(10f).Within(0.001f));
        Assert.That(BoundaryMath.ArenaMassAbilityVelocityChange(1f), Is.EqualTo(30f).Within(0.001f));
    }

    [Test]
    public void ArenaCubesAndGroundBlackHoles_AreLethalOnContact()
    {
        Assert.That(BoundaryMath.IsLethalContactHazard(BoundaryHazardKind.Cube, true), Is.True);
        Assert.That(BoundaryMath.IsLethalContactHazard(BoundaryHazardKind.ArenaBlackHole, true), Is.True);
        Assert.That(BoundaryMath.IsLethalContactHazard(BoundaryHazardKind.ArenaBlackHole, false), Is.False);
    }
}
#endif
