using NUnit.Framework;
using UnityEngine;

public sealed class BullseyeAbilityTests
{
    [Test]
    public void UsesRequestedCooldownAndDamageBands()
    {
        Assert.That(BullseyeAbility.CooldownSeconds, Is.EqualTo(2f));
        // Bands are normalized against BullseyeAbility.TargetRadius (the ring's own drawn
        // radius) as of the fix that made hit detection match the on-screen reticle -
        // CenterRadius=0.30, RingRadius=1.0 (previously 0.60/3.0 when this was normalized
        // against whichever collider the shot happened to hit instead of a fixed target).
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(0f), Is.EqualTo(12f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(0.30f), Is.EqualTo(12f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(0.31f), Is.EqualTo(7f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(1f), Is.EqualTo(7f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(1.01f), Is.Zero);
    }

    [Test]
    public void TargetOffsetNormalizesAgainstFixedTargetRadius()
    {
        // NormalizedTargetOffset now scores against a caller-supplied centre/radius (the
        // ring's own centre and drawn radius) instead of the struck collider's bounds, so
        // the result depends on the shot alone, not on which of the player's several
        // overlapping colliders happened to be hit.
        Vector3 targetCenter = Vector3.zero;
        const float targetRadius = 2f;
        Assert.That(BullseyeAbility.NormalizedTargetOffset(
                new Vector3(0f, 1f, -1f), targetCenter, targetRadius, Vector3.forward),
            Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void TargetCenterTracksScaledPlayerGeometry()
    {
        Vector3 playerPosition = new Vector3(2f, 3f, 4f);
        Assert.That(BullseyeAbility.TargetCenter(playerPosition), Is.EqualTo(
            playerPosition + Vector3.up *
            PlayerMovement.ScaleDistance(BullseyeAbility.TargetCenterHeight)));
    }

    [Test]
    public void ReticleCancelsPlayerRootScaleSoWorldRadiusMatchesHitRadius()
    {
        float localCompensation = BullseyeTargetPresentation.SafeReciprocal(
            PlayerMovement.CharacterScale);
        float renderedWorldRadius = BullseyeTargetPresentation.OuterRingRadius *
            localCompensation * PlayerMovement.CharacterScale;
        Assert.That(renderedWorldRadius, Is.EqualTo(BullseyeAbility.TargetRadius).Within(0.001f));
    }

    [Test]
    public void InnerRingMatchesCenterDamageBoundary()
    {
        Assert.That(BullseyeTargetPresentation.InnerRingRadius,
            Is.EqualTo(BullseyeAbility.TargetRadius * BullseyeAbility.CenterRadius)
                .Within(0.001f));
        Assert.That(BullseyeAbility.DamageForNormalizedTargetOffset(
            BullseyeTargetPresentation.InnerRingRadius / BullseyeAbility.TargetRadius),
            Is.EqualTo(BullseyeAbility.CenterDamage));
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
