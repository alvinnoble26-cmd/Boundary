using NUnit.Framework;
using UnityEngine;

public sealed class BullseyeAbilityTests
{
    [Test]
    public void UsesRequestedCooldownAndDamageBands()
    {
        Assert.That(BullseyeAbility.CooldownSeconds, Is.EqualTo(2f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(0f), Is.EqualTo(12f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(0.3f), Is.EqualTo(12f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(0.4f), Is.EqualTo(7f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(1f), Is.EqualTo(7f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(1.01f), Is.Zero);
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
}
