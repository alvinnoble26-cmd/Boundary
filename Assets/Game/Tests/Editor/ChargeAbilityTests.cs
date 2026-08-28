using NUnit.Framework;
using UnityEngine;

public sealed class ChargeAbilityTests
{
    [Test]
    public void UsesRequestedTimingDamageAndRadius()
    {
        Assert.That(ChargeAbility.CooldownSeconds, Is.EqualTo(7f));
        Assert.That(ChargeAbility.ChargeSeconds, Is.EqualTo(0.5f));
        Assert.That(ChargeAbility.FirstTickDelay, Is.EqualTo(2f));
        Assert.That(ChargeAbility.SecondTickDelay, Is.EqualTo(2f));
        Assert.That(ChargeAbility.FirstTickDamage, Is.EqualTo(5f));
        Assert.That(ChargeAbility.SecondTickDamage, Is.EqualTo(7f));
        Assert.That(ChargeAbility.ExplosionRadius, Is.EqualTo(10f));
        Assert.That(ChargeAbility.HitEffectDuration, Is.EqualTo(2f));
        Assert.That(ChargeAbility.ProjectileSpeed, Is.EqualTo(36f));
    }

    [Test]
    public void ChargeOriginCanRemainAttachedToMovingPlayerUntilLaunch()
    {
        GameObject anchorObject = new GameObject("Charge Anchor Test");
        try
        {
            anchorObject.transform.position = new Vector3(2f, 0f, 3f);
            Vector3 origin = new Vector3(2.25f, 1f, 3.5f);
            Vector3 localOrigin = ChargeAbility.GetAnchoredChargeOrigin(
                anchorObject.transform, origin);

            anchorObject.transform.position += new Vector3(4f, 0f, -2f);

            Assert.That(anchorObject.transform.TransformPoint(localOrigin),
                Is.EqualTo(origin + new Vector3(4f, 0f, -2f)));
        }
        finally
        {
            Object.DestroyImmediate(anchorObject);
        }
    }

    [Test]
    public void ExplosionIncludesBoundaryAndRejectsOutsidePoint()
    {
        Assert.That(ChargeAbility.IsInsideExplosion(new Vector3(10f, 0f, 0f), Vector3.zero), Is.True);
        Assert.That(ChargeAbility.IsInsideExplosion(new Vector3(10.01f, 0f, 0f), Vector3.zero), Is.False);
    }

    [Test]
    public void StaffChargeOriginIsInsetFromUpperTip()
    {
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(1f, 2f, 1f));

        Vector3 origin = ChargeAbility.GetStaffChargeOrigin(bounds, Vector3.up, Vector3.forward);

        Assert.That(origin.y, Is.EqualTo(0.72f).Within(0.0001f));
        Assert.That(origin.z, Is.EqualTo(0.045f).Within(0.0001f));
    }

    [Test]
    public void ContactCenterUsesSphereCastTravelDistanceWithoutAnExtraRadiusGap()
    {
        Vector3 contactCenter = ChargeAbility.GetContactCenter(
            new Vector3(2f, 3f, 4f), Vector3.forward * 5f, 1.25f);

        Assert.That(contactCenter, Is.EqualTo(new Vector3(2f, 3f, 5.25f)));
    }

    [Test]
    public void ContactCenterDoesNotMoveBackwardForAnInitialOverlap()
    {
        Vector3 start = new Vector3(2f, 3f, 4f);

        Assert.That(ChargeAbility.GetContactCenter(start, Vector3.forward, -0.5f),
            Is.EqualTo(start));
    }
}
