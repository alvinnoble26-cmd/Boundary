using NUnit.Framework;
using UnityEngine;

public sealed class HollowAbilityTests
{
    [Test]
    public void CooldownIsFiveSeconds()
    {
        Assert.AreEqual(5f, HollowAbility.CooldownSeconds, 0.001f);
    }

    [Test]
    public void ChargeDurationIsThreeQuartersOfASecond()
    {
        Assert.AreEqual(0.75f, HollowAbility.ChargeDuration, 0.001f);
    }

    [Test]
    public void ChargePresentationIsLowerThanBlastOrigin()
    {
        Vector3 playerPosition = new Vector3(2f, 3f, 4f);
        Vector3 blastOrigin = HollowAbility.GetBlastOrigin(playerPosition, Vector3.forward);
        Vector3 chargePosition = HollowAbility.GetChargePresentationPosition(
            playerPosition, Vector3.forward);

        Assert.AreEqual(blastOrigin.x, chargePosition.x, 0.001f);
        Assert.AreEqual(blastOrigin.z, chargePosition.z, 0.001f);
        Assert.AreEqual(blastOrigin.y + HollowAbility.ChargePresentationVerticalOffset,
            chargePosition.y, 0.001f);
    }

    [Test]
    public void BlastRadiusIsTripledFromOriginalSize()
    {
        Assert.AreEqual(HollowAbility.InitialBlastRadius * 3f, HollowAbility.BlastRadius, 0.001f);
    }

    [Test]
    public void BlastRadiusWidensLinearlyAcrossRange()
    {
        Assert.AreEqual(2.4f, HollowAbility.GetBlastRadius(0f), 0.001f);
        Assert.AreEqual(4.8f, HollowAbility.GetBlastRadius(30f), 0.001f);
        Assert.AreEqual(7.2f, HollowAbility.GetBlastRadius(60f), 0.001f);
    }

    [Test]
    public void BlastRejectsWidePointNearOriginButIncludesItAtMaximumRange()
    {
        Assert.IsFalse(HollowAbility.IsPointInsideBlast(
            new Vector3(6f, 0f, 1f), Vector3.zero, Vector3.forward));
        Assert.IsTrue(HollowAbility.IsPointInsideBlast(
            new Vector3(6f, 0f, 60f), Vector3.zero, Vector3.forward));
    }

    [Test]
    public void BlastIncludesPointAlongAxisWithinRange()
    {
        Assert.IsTrue(HollowAbility.IsPointInsideBlast(
            new Vector3(0f, 0f, 59.9f), Vector3.zero, Vector3.forward));
    }

    [Test]
    public void BlastRejectsPointBeyondMaximumRange()
    {
        Assert.IsFalse(HollowAbility.IsPointInsideBlast(
            new Vector3(0f, 0f, 60.1f), Vector3.zero, Vector3.forward));
    }

    [Test]
    public void BlastRejectsPointOutsideWidth()
    {
        Assert.IsFalse(HollowAbility.IsPointInsideBlast(
            new Vector3(HollowAbility.BlastRadius + 0.01f, 0f, 20f), Vector3.zero, Vector3.forward));
    }

    [Test]
    public void FullBlastDealsFortyDamage()
    {
        Assert.AreEqual(40f, HollowAbility.DamagePerSecond * HollowAbility.BlastDuration, 0.001f);
    }

    [Test]
    public void HollowGlowSelectsEveryArenaMass()
    {
        int glowing = 0;
        const int population = 100;
        for (int index = 0; index < population; index++)
            if (BoundaryHazard.ShouldGlowDuringHollow(index))
                glowing++;

        Assert.AreEqual(population, glowing);
    }
}

public sealed class PlayerWindPresentationTests
{
    [Test]
    public void PerimeterSequenceCyclesAcrossAllFourScreenEdges()
    {
        const float inset = 0.02f;
        Vector2 left = PlayerWindPresentation.PerimeterViewportPosition(0, inset);
        Vector2 top = PlayerWindPresentation.PerimeterViewportPosition(1, inset);
        Vector2 right = PlayerWindPresentation.PerimeterViewportPosition(2, inset);
        Vector2 bottom = PlayerWindPresentation.PerimeterViewportPosition(3, inset);

        Assert.That(left.x, Is.EqualTo(inset).Within(0.0001f));
        Assert.That(top.y, Is.EqualTo(1f - inset).Within(0.0001f));
        Assert.That(right.x, Is.EqualTo(1f - inset).Within(0.0001f));
        Assert.That(bottom.y, Is.EqualTo(inset).Within(0.0001f));
    }

    [Test]
    public void PerimeterSequenceDistributesSuccessiveLapsAlongEachEdge()
    {
        Vector2 first = PlayerWindPresentation.PerimeterViewportPosition(0, 0.02f);
        Vector2 nextLap = PlayerWindPresentation.PerimeterViewportPosition(4, 0.02f);

        Assert.That(Mathf.Abs(first.y - nextLap.y), Is.GreaterThan(0.1f));
    }

    [Test]
    public void ForwardMovementFlowsOutwardFromScreenCenter()
    {
        Vector2 radial = new Vector2(1f, 0.25f).normalized;
        Vector2 flow = PlayerWindPresentation.ScreenFlowDirection(Vector3.forward, radial);

        Assert.That(Vector2.Dot(flow, radial), Is.GreaterThan(0.99f));
    }

    [Test]
    public void BackwardMovementFlowsInwardTowardScreenCenter()
    {
        Vector2 radial = new Vector2(-0.4f, 1f).normalized;
        Vector2 flow = PlayerWindPresentation.ScreenFlowDirection(Vector3.back, radial);

        Assert.That(Vector2.Dot(flow, radial), Is.LessThan(-0.99f));
    }

    [Test]
    public void RightStrafeMakesWindFlowLeft()
    {
        Vector2 flow = PlayerWindPresentation.ScreenFlowDirection(Vector3.right, Vector2.up);

        Assert.That(flow.x, Is.LessThan(-0.99f));
    }
}

public sealed class BoundaryCubePresentationTests
{
    [Test]
    public void CubeUsesStrongBlueEmissionInEveryPresentationState()
    {
        Color normal = BoundaryHazard.CubeGlowColor(false, false);
        Color hollow = BoundaryHazard.CubeGlowColor(true, false);
        Color darkness = BoundaryHazard.CubeGlowColor(false, true);

        Assert.That(normal.b, Is.GreaterThan(normal.r));
        Assert.That(normal.b, Is.GreaterThan(normal.g));
        Assert.That(hollow.b, Is.GreaterThan(hollow.r));
        Assert.That(darkness.b, Is.GreaterThan(darkness.r));
        Assert.That(BoundaryHazard.CubeGlowIntensity(false, false), Is.GreaterThanOrEqualTo(18f));
        Assert.That(BoundaryHazard.CubeGlowIntensity(true, false), Is.GreaterThan(18f));
        Assert.That(BoundaryHazard.CubeGlowIntensity(false, true), Is.GreaterThan(18f));
    }
}

public sealed class ForceFieldWindPresentationTests
{
    [Test]
    public void AttractAndRepelWindVisualsUseSixtyUnitRadius()
    {
        Assert.That(ForceField.WindVisualRadius, Is.EqualTo(60f).Within(0.001f));
        Assert.That(ForceField.MiniStarPointCount, Is.EqualTo(5));
    }

    [Test]
    public void WindDirectionsAreNormalizedAndDistributedInThreeDimensions()
    {
        Vector3 first = ForceField.FieldWindDirection(0);
        Vector3 second = ForceField.FieldWindDirection(1);
        Vector3 later = ForceField.FieldWindDirection(17);

        Assert.That(first.magnitude, Is.EqualTo(1f).Within(0.001f));
        Assert.That(second.magnitude, Is.EqualTo(1f).Within(0.001f));
        Assert.That(later.magnitude, Is.EqualTo(1f).Within(0.001f));
        Assert.That(Vector3.Dot(first, second), Is.LessThan(0.95f));
        Assert.That(Vector3.Dot(first, later), Is.LessThan(0.95f));
    }

    [Test]
    public void AttractFlashUsesSixtyUnitRadiusAndTripledBrightPeak()
    {
        Assert.That(ForceField.AttractFlashRadius, Is.EqualTo(60f).Within(0.001f));
        Assert.That(ForceField.VisualBrightnessMultiplier, Is.EqualTo(6f).Within(0.001f));
        Assert.That(ForceField.AttractFlashPeakIntensity, Is.EqualTo(5400f).Within(0.001f));
        Assert.That(ForceField.AttractFlashDuration, Is.EqualTo(2.1f).Within(0.001f));
    }

    [Test]
    public void RepelFlashUsesSixtyUnitRadiusAndTripledBrightPeak()
    {
        Assert.That(ForceField.RepelFlashRadius, Is.EqualTo(60f).Within(0.001f));
        Assert.That(ForceField.RepelFlashPeakIntensity, Is.EqualTo(5400f).Within(0.001f));
        Assert.That(ForceField.RepelFlashDuration, Is.EqualTo(2.1f).Within(0.001f));
    }
}
