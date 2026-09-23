using NUnit.Framework;
using UnityEngine;

public sealed class BaseAbilityTests
{
    [Test]
    public void SliceHazardRepelIsBoundedAndFallsOffWithDistance()
    {
        float nearby = SliceAbility.HazardRepelVelocityChange(1f, 2.5f);
        float distant = SliceAbility.HazardRepelVelocityChange(0.25f, 2.5f);

        Assert.That(nearby, Is.EqualTo(SliceAbility.HazardRepelMaxVelocityChange).Within(0.001f));
        Assert.That(distant, Is.GreaterThan(0f).And.LessThan(nearby));
    }

    [Test]
    public void UsesRequestedChargesTimingAndSize()
    {
        Assert.That(BaseAbility.ChargeCount, Is.EqualTo(2));
        Assert.That(BaseAbility.CooldownSeconds, Is.EqualTo(4f));
        Assert.That(BaseAbility.PlatformLifetime, Is.EqualTo(1f));
        Assert.That(BaseAbility.PlatformRadius, Is.EqualTo(8.25f));
    }

    [Test]
    public void ChargesRechargeIndependentlyFromTheirOwnActivationTimes()
    {
        float[] cooldownEnds = { 4f, 4.6f };

        Assert.That(BaseAbility.FindReadyCharge(3.99f, cooldownEnds), Is.EqualTo(-1));
        Assert.That(BaseAbility.FindReadyCharge(4f, cooldownEnds), Is.EqualTo(0));
        Assert.That(BaseAbility.FindReadyCharge(4.6f, cooldownEnds), Is.EqualTo(0));
    }

    [Test]
    public void PlatformIsCenteredBelowSubmittedPlayerFeet()
    {
        Vector3 authoritativePosition = new Vector3(2f, 10f, 4f);
        Vector3 submittedPosition = new Vector3(2.2f, 9.8f, 4.1f);
        Bounds playerBounds = new Bounds(
            new Vector3(2f, 10f, 4f),
            new Vector3(1.3f, 1.3f, 1.3f));

        Vector3 center = BaseAbility.PlatformCenterForPlayer(
            submittedPosition, authoritativePosition, playerBounds);

        Assert.That(center.x, Is.EqualTo(submittedPosition.x));
        Assert.That(center.z, Is.EqualTo(submittedPosition.z));
        float expectedTop = submittedPosition.y - 0.65f - BaseAbility.SurfaceGap;
        Assert.That(center.y + BaseAbility.PlatformThickness * 0.5f,
            Is.EqualTo(expectedTop).Within(0.0001f));
    }
}
