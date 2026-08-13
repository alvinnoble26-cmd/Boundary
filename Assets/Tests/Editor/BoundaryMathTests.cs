#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class BoundaryMathTests
{
    [Test]
    public void TransitionRadius_IsSmoothAndHitsBothEndpoints()
    {
        Assert.That(BoundaryMath.TransitionRadius(40f, 22f, 0f, 7f), Is.EqualTo(40f).Within(0.001f));
        Assert.That(BoundaryMath.TransitionRadius(40f, 22f, 7f, 7f), Is.EqualTo(22f).Within(0.001f));
        Assert.That(BoundaryMath.TransitionRadius(40f, 22f, 3.5f, 7f), Is.EqualTo(31f).Within(0.001f));
    }

    [Test]
    public void GroundingAndBracing_MeaningfullyReducePull()
    {
        Vector3 player = Vector3.zero;
        Vector3 singularity = new Vector3(0f, 28f, 0f);
        Vector3 center = Vector3.zero;
        Vector3 airborne = BoundaryMath.PlayerPullAcceleration(
            player, singularity, center, -1f, 40f, 12f, false, false, 1f);
        Vector3 grounded = BoundaryMath.PlayerPullAcceleration(
            player, singularity, center, -1f, 40f, 12f, true, false, 1f);
        Vector3 braced = BoundaryMath.PlayerPullAcceleration(
            player, singularity, center, -1f, 40f, 12f, true, true, 0.25f);

        Assert.That(grounded.magnitude, Is.LessThan(airborne.magnitude * 0.2f));
        Assert.That(braced.magnitude, Is.LessThan(grounded.magnitude * 0.4f));
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
            true,
            true,
            0.25f);

        Assert.That(acceleration.x, Is.LessThan(-2f));
        Assert.That(acceleration.y, Is.GreaterThan(4f));
    }

    [Test]
    public void GravityPulse_HasWarningPeakAndRecovery()
    {
        Assert.That(BoundaryMath.RhythmicPulse(0.5f, 4f, 1f, 1f), Is.Zero);
        Assert.That(BoundaryMath.RhythmicPulse(1.5f, 4f, 1f, 1f), Is.EqualTo(1f).Within(0.001f));
        Assert.That(BoundaryMath.RhythmicPulse(2.5f, 4f, 1f, 1f), Is.Zero);
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
}
#endif
