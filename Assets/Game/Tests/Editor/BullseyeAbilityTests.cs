using NUnit.Framework;
using UnityEngine;

public sealed class BullseyeAbilityTests
{
    [Test]
    public void UsesRequestedCooldownAndDamageBands()
    {
        Assert.That(BullseyeAbility.CooldownSeconds, Is.EqualTo(2f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(0f), Is.EqualTo(12f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(0.6f), Is.EqualTo(12f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(0.61f), Is.EqualTo(7f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(3f), Is.EqualTo(7f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(3.01f), Is.Zero);
    }

    [Test]
    public void TargetOffsetNormalizesAgainstColliderBounds()
    {
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(2f, 4f, 2f));
        Assert.That(BullseyeAbility.NormalizedTargetOffset(
                new Vector3(0f, 1f, -1f), bounds, Vector3.forward),
            Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void KnifeTipFacesItsTravelDirection()
    {
        Vector3 direction = new Vector3(0.2f, 0.35f, 0.9f).normalized;
        Quaternion rotation = BullseyeAbility.KnifeRotationForDirection(direction);

        Assert.That(Vector3.Angle(rotation * Vector3.up, direction), Is.LessThan(0.001f));
    }

    [Test]
    public void CrosshairShotStartsAtCameraAndPassesThroughCrosshairAimPoint()
    {
        Vector3 cameraPosition = new Vector3(0f, 1.6f, 0f);
        Vector3 cameraForward = new Vector3(0.1f, -0.05f, 1f).normalized;
        Vector3 handPosition = new Vector3(0.55f, 1.1f, 0.7f);
        Vector3 aimPoint = cameraPosition + cameraForward * 30f;

        BullseyeAbility.ResolveCrosshairShot(cameraPosition, cameraForward,
            handPosition, Vector3.forward, out Vector3 origin, out Vector3 direction);

        Assert.That(origin, Is.EqualTo(cameraPosition));
        Assert.That(Vector3.Cross(aimPoint - origin, direction).magnitude,
            Is.LessThan(0.0001f));
    }

    [Test]
    public void CrosshairShotDoesNotUseOffsetHandOrigin()
    {
        Vector3 cameraPosition = new Vector3(0f, 1.6f, 0f);
        Vector3 handPosition = new Vector3(0.55f, 1.1f, 0.7f);

        BullseyeAbility.ResolveCrosshairShot(cameraPosition, Vector3.forward,
            handPosition, Vector3.forward, out Vector3 origin, out _);

        Assert.That(origin, Is.Not.EqualTo(handPosition));
        Assert.That(origin, Is.EqualTo(cameraPosition));
    }
}
