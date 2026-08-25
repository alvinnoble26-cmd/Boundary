using UnityEngine;

public enum BoundaryPhase
{
    Waiting,
    OuterRing,
    MiddleRing,
    InnerRing
}

public enum BoundaryTransition
{
    None,
    ClosingOuterRing,
    ClosingMiddleRing
}

public enum BoundaryDisaster
{
    None,
    BlackRain,
    CubeStorm,
    GravitySurge,
    OrbitalStrike,
    FractureLines,
    DarkMatterFog,
    MeteorBreak,
    UnstableMass
}

public enum BoundaryDisasterStage
{
    None,
    Warning,
    Active,
    Recovery
}

public enum BoundaryKnockoutState
{
    Grounded,
    Airborne,
    EventHorizon,
    OutOfBounds,
    Consumed
}

public enum BoundaryHazardKind
{
    Cube,
    BlackRainSingularity,
    OrbitalDebris,
    Meteor,
    FalseSingularity,
    TornadoDebris,
    ArenaBlackHole
}

public static class BoundaryMath
{
    public const float MaximumHealth = 100f;
    public const float BlackHoleDamagePerSecond = 60f;

    public static float BlackHoleDamage(float contactSeconds)
    {
        return Mathf.Max(0f, contactSeconds) * BlackHoleDamagePerSecond;
    }

    public static float ApplyDamage(float currentHealth, float damage)
    {
        return Mathf.Clamp(currentHealth - Mathf.Max(0f, damage), 0f, MaximumHealth);
    }

    public static bool SurvivesInitialBoundaryCollapse(int arenaMassVariant)
    {
        // Arena cubes use variants 0-9 and black holes use 10-19. Keeping the
        // even variants leaves exactly five of each kind after the first ring
        // collapse, without needing any client-side random selection.
        return (arenaMassVariant & 1) == 0;
    }

    public static float OutOfBoundsMargin(BoundaryPhase phase)
    {
        switch (phase)
        {
            case BoundaryPhase.OuterRing: return 5f;
            case BoundaryPhase.MiddleRing: return 3f;
            case BoundaryPhase.InnerRing: return 2f;
            default: return float.PositiveInfinity;
        }
    }

    public static float EaseInOut(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    public static float TransitionRadius(float from, float to, float elapsed, float duration)
    {
        if (duration <= 0f)
            return to;

        return Mathf.Lerp(from, to, EaseInOut(elapsed / duration));
    }

    /// <summary>
    /// Returns a readable surge curve: a short warning trough followed by a
    /// smooth, dangerous pulse. Zero means normal gravity and one is peak.
    /// </summary>
    public static float RhythmicPulse(float elapsed, float interval, float warningDuration, float pulseDuration)
    {
        if (elapsed < 0f || interval <= 0f || pulseDuration <= 0f)
            return 0f;

        float cycle = Mathf.Repeat(elapsed, interval);
        if (cycle < warningDuration)
            return 0f;

        float pulseTime = cycle - warningDuration;
        if (pulseTime >= pulseDuration)
            return 0f;

        return Mathf.Sin(Mathf.Clamp01(pulseTime / pulseDuration) * Mathf.PI);
    }

    public static float DisasterPower(BoundaryDisaster disaster)
    {
        switch (disaster)
        {
            case BoundaryDisaster.BlackRain: return 1.35f;
            case BoundaryDisaster.CubeStorm: return 1.4f;
            case BoundaryDisaster.GravitySurge: return 1.4f;
            case BoundaryDisaster.OrbitalStrike: return 1.35f;
            case BoundaryDisaster.FractureLines: return 1.4f;
            case BoundaryDisaster.DarkMatterFog: return 1.35f;
            case BoundaryDisaster.MeteorBreak: return 1.4f;
            case BoundaryDisaster.UnstableMass: return 1.5f;
            default: return 1f;
        }
    }

    public static bool IsBelowVoidKillPlane(float playerY, float arenaFloorY, float killDepth)
    {
        return playerY <= arenaFloorY - Mathf.Max(1f, killDepth);
    }

    public static float ArenaMassAbilityVelocityChange(float influence)
    {
        return FieldVelocityChange(220f, 88f, influence, 2.5f);
    }

    public static float FieldVelocityChange(
        float fieldForce,
        float fieldAcceleration,
        float distanceInfluence,
        float rigidbodyMass)
    {
        float influence = Mathf.Clamp01(distanceInfluence);
        float mass = Mathf.Max(0.1f, rigidbodyMass);
        float forceResponse = Mathf.Max(0f, fieldForce) / mass;
        float accelerationLimit = Mathf.Max(0f, fieldAcceleration);
        return Mathf.Min(forceResponse, accelerationLimit) * influence;
    }

    public static bool IsLethalContactHazard(BoundaryHazardKind kind, bool isArenaMass)
    {
        return false;
    }

    public static float DensePlatformSpacing(float platformSize, float seamOverlap)
    {
        return Mathf.Max(0.5f, platformSize - Mathf.Max(0.05f, seamOverlap));
    }

    public static float TierRampSlopeDegrees(float heightDifference, float rampLength)
    {
        return Mathf.Atan2(Mathf.Abs(heightDifference), Mathf.Max(0.1f, rampLength)) * Mathf.Rad2Deg;
    }

    public static float StableUnit(int seed, int index)
    {
        return StableHash(seed, index) / (float)int.MaxValue;
    }

    public static float SingularityProximity01(
        Vector3 playerPosition,
        Vector3 singularityPosition,
        float nearDistance = 7f,
        float farDistance = 42f)
    {
        float distance = Vector3.Distance(playerPosition, singularityPosition);
        float proximity = Mathf.InverseLerp(
            Mathf.Max(nearDistance + 0.1f, farDistance),
            Mathf.Max(0.1f, nearDistance),
            distance);
        return EaseInOut(proximity);
    }

    public static float BoundaryFallGravityMultiplier(float singularityProximity)
    {
        // Falling remains responsive away from the core, then progressively
        // lightens beneath it so its upward pull is not erased by 12x gravity.
        return Mathf.Lerp(2.45f, 0.85f, Mathf.Clamp01(singularityProximity));
    }

    public static Vector3 PlayerPullAcceleration(
        Vector3 playerPosition,
        Vector3 singularityPosition,
        Vector3 arenaCenter,
        float arenaFloorY,
        float ringRadius,
        float basePull,
        bool stableGrounded)
    {
        Vector3 toSingularity = singularityPosition - playerPosition;
        if (toSingularity.sqrMagnitude < 0.01f || basePull <= 0f)
            return Vector3.zero;

        Vector3 flatOffset = playerPosition - arenaCenter;
        flatOffset.y = 0f;
        float horizontalDistance = flatOffset.magnitude;
        bool outsideBoundary = horizontalDistance > Mathf.Max(1f, ringRadius);

        float height01 = Mathf.InverseLerp(arenaFloorY + 1f, singularityPosition.y - 4f, playerPosition.y);
        float altitudeMultiplier = Mathf.Lerp(0.85f, 2.15f, height01);
        float edge01 = Mathf.InverseLerp(ringRadius * 0.80f, ringRadius * 1.10f, horizontalDistance);
        float edgeMultiplier = Mathf.Lerp(1f, 1.42f, edge01);
        float footingMultiplier = stableGrounded && !outsideBoundary ? 0.055f : 1f;
        float proximityMultiplier = Mathf.Lerp(
            1f,
            1.3f,
            SingularityProximity01(playerPosition, singularityPosition));

        Vector3 pull = toSingularity.normalized *
                       (basePull * altitudeMultiplier * edgeMultiplier * footingMultiplier *
                        proximityMultiplier);

        // Once a section has closed, the old floor is no longer safe. This is
        // intentionally recoverable: it pushes inward and upward instead of
        // killing a player for crossing an invisible line.
        if (outsideBoundary)
        {
            Vector3 inward = flatOffset.sqrMagnitude > 0.01f ? -flatOffset.normalized : Vector3.zero;
            float outsideDistance = horizontalDistance - ringRadius;
            pull += inward * Mathf.Min(19f, 5.5f + outsideDistance * 1.05f);
        }

        return Vector3.ClampMagnitude(pull, 50f);
    }

    public static int StableHash(int seed, int index)
    {
        unchecked
        {
            uint value = (uint)(seed + index * 0x1f123bb5);
            value ^= value >> 16;
            value *= 0x7feb352d;
            value ^= value >> 15;
            value *= 0x846ca68b;
            value ^= value >> 16;
            return (int)(value & 0x7fffffff);
        }
    }

    public static string DisasterName(BoundaryDisaster disaster)
    {
        switch (disaster)
        {
            case BoundaryDisaster.BlackRain: return "BLACK RAIN";
            case BoundaryDisaster.CubeStorm: return "CUBE STORM";
            case BoundaryDisaster.GravitySurge: return "GRAVITY SURGE";
            case BoundaryDisaster.OrbitalStrike: return "ORBITAL STRIKE";
            case BoundaryDisaster.FractureLines: return "FRACTURE LINES";
            case BoundaryDisaster.DarkMatterFog: return "DARK MATTER FOG";
            case BoundaryDisaster.MeteorBreak: return "METEOR BREAK";
            case BoundaryDisaster.UnstableMass: return "UNSTABLE MASS";
            default: return string.Empty;
        }
    }

    public static string DisasterHint(BoundaryDisaster disaster)
    {
        switch (disaster)
        {
            case BoundaryDisaster.BlackRain:
                return "Redirect the falling wells—or pull your rival into one.";
            case BoundaryDisaster.CubeStorm:
                return "Turn the falling cubes into weapons with Attract and Repel.";
            case BoundaryDisaster.GravitySurge:
                return "Keep your footing through each pulse, then punish airborne rivals.";
            case BoundaryDisaster.OrbitalStrike:
                return "Read the orbit, slide beneath it, or redirect its next pass.";
            case BoundaryDisaster.FractureLines:
                return "Glowing seams lift in sequence. Force your rival across one.";
            case BoundaryDisaster.DarkMatterFog:
                return "Silhouettes stay bright. Close distance and hide your throws.";
            case BoundaryDisaster.MeteorBreak:
                return "Impact zones become debris. Bait a rival in, then launch the remains.";
            case BoundaryDisaster.UnstableMass:
                return "Every cube is charged. Watch its glow before the mass pulses.";
            default:
                return string.Empty;
        }
    }
}
