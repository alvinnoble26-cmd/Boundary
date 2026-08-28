using NUnit.Framework;
using UnityEngine;

public sealed class SliceAbilityTests
{
    [Test]
    public void UsesRequestedBalanceAndPresentationTiming()
    {
        Assert.That(SliceAbility.CooldownSeconds, Is.EqualTo(1f));
        Assert.That(SliceAbility.Damage, Is.EqualTo(7f));
        Assert.That(SliceAbility.Radius, Is.EqualTo(4f));
        Assert.That(SliceAbility.ArcDegrees, Is.EqualTo(120f));
        Assert.That(SliceAbility.SwingDuration, Is.EqualTo(0.14f));
        Assert.That(SliceAbility.ScreenSliceDuration, Is.EqualTo(0.75f));
        Assert.That(SliceAirFracture.FullyVisibleSeconds, Is.EqualTo(3f));
        Assert.That(SliceAirFracture.LifetimeSeconds, Is.EqualTo(3.35f).Within(0.0001f));
    }

    [Test]
    public void ForwardArcIncludesEdgeAndRejectsBehindTarget()
    {
        Vector3 edge = Quaternion.Euler(0f, 60f, 0f) * Vector3.forward * 4f;
        Assert.That(SliceAbility.IsInSlash(Vector3.zero, Vector3.forward, edge), Is.True);
        Assert.That(SliceAbility.IsInSlash(Vector3.zero, Vector3.forward, Vector3.back), Is.False);
        Assert.That(SliceAbility.IsInSlash(Vector3.zero, Vector3.forward, Vector3.forward * 4.01f), Is.False);
    }

    [Test]
    public void ScreenOverlayIsRestrictedToCasterOwnerOnConfirmedHit()
    {
        Assert.That(SliceAbility.ShouldShowScreenOverlay(true, true), Is.True);
        Assert.That(SliceAbility.ShouldShowScreenOverlay(true, false), Is.False);
        Assert.That(SliceAbility.ShouldShowScreenOverlay(false, true), Is.False);
    }

    [Test]
    public void ReconstructedFractureUsesOnlyItsRemainingLifetime()
    {
        Assert.That(SliceAirFracture.RemainingLifetime(1.25f), Is.EqualTo(1.75f).Within(0.001f));
        Assert.That(SliceAirFracture.RemainingLifetime(4f), Is.Zero);
        Assert.That(SliceAirFracture.RemainingLifetime(-1f), Is.EqualTo(3f).Within(0.001f));
    }
}
