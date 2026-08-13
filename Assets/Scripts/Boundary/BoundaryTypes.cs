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
    UnstableMass,
    FalseSingularities
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

    public static bool IsBelowVoidKillPlane(float playerY, float arenaFloorY, float killDepth)
    {
        return playerY <= arenaFloorY - Mathf.Max(1f, killDepth);
    }

    public static float ArenaMassAbilityVelocityChange(float influence)
    {
        return Mathf.Lerp(16f, 44f, Mathf.Clamp01(influence));
    }

    public static bool IsLethalContactHazard(BoundaryHazardKind kind, bool isArenaMass)
    {
        return kind == BoundaryHazardKind.Cube ||
               (isArenaMass && kind == BoundaryHazardKind.ArenaBlackHole);
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

        Vector3 pull = toSingularity.normalized *
                       (basePull * altitudeMultiplier * edgeMultiplier * footingMultiplier);

        // Once a section has closed, the old floor is no longer safe. This is
        // intentionally recoverable: it pushes inward and upward instead of
        // killing a player for crossing an invisible line.
        if (outsideBoundary)
        {
            Vector3 inward = flatOffset.sqrMagnitude > 0.01f ? -flatOffset.normalized : Vector3.zero;
            float outsideDistance = horizontalDistance - ringRadius;
            pull += inward * Mathf.Min(19f, 5.5f + outsideDistance * 1.05f);
            pull += Vector3.up * Mathf.Min(22f, 7f + outsideDistance * 1.20f);
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
            case BoundaryDisaster.FalseSingularities: return "FALSE SINGULARITIES";
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
            case BoundaryDisaster.FalseSingularities:
                return "Only one well is real. Feed it debris to expose and strengthen it.";
            default:
                return string.Empty;
        }
    }
}
