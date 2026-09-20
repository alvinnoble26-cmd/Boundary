using System;
using UnityEngine;

/// <summary>Pure decision math for local CPU practice.</summary>
public static class BoundaryCpuRules
{
    public static AbilityId[] RandomLoadout(int seed)
    {
        AbilityId[] pool = (AbilityId[])Enum.GetValues(typeof(AbilityId));
        int count = 0;
        foreach (AbilityId id in pool)
            if (LoadoutManager.IsAbilityEnabled(id)) pool[count++] = id;
        System.Random random = new System.Random(seed);
        for (int i = count - 1; i > 0; i--)
        {
            int swap = random.Next(i + 1);
            (pool[i], pool[swap]) = (pool[swap], pool[i]);
        }
        return new[] { pool[0], pool[1], pool[2] };
    }

    public static int LoadoutMask(AbilityId[] loadout)
    {
        int mask = 0;
        if (loadout == null)
            return mask;
        for (int index = 0; index < loadout.Length; index++)
            mask |= 1 << (int)loadout[index];
        return mask;
    }

    public static Vector3 LeadTarget(Vector3 origin, Vector3 target, Vector3 targetVelocity,
        float projectileSpeed, float windup = 0f)
    {
        Vector3 delayedTarget = target + targetVelocity * windup;
        Vector3 offset = delayedTarget - origin;
        float a = targetVelocity.sqrMagnitude - projectileSpeed * projectileSpeed;
        float b = 2f * Vector3.Dot(offset, targetVelocity);
        float c = offset.sqrMagnitude;
        float time = 0f;
        if (Mathf.Abs(a) < 0.001f)
            time = Mathf.Abs(b) > 0.001f ? -c / b : 0f;
        else
        {
            float discriminant = b * b - 4f * a * c;
            if (discriminant >= 0f)
            {
                float first = (-b - Mathf.Sqrt(discriminant)) / (2f * a);
                float second = (-b + Mathf.Sqrt(discriminant)) / (2f * a);
                time = first > 0f && second > 0f ? Mathf.Min(first, second) : Mathf.Max(first, second);
            }
        }
        return delayedTarget + targetVelocity * Mathf.Clamp(time, 0f, 2f);
    }

    public static float MovingThreat(Vector3 position, Vector3 velocity, Vector3 threatPosition,
        Vector3 threatVelocity, float radius, float horizon)
    {
        Vector3 offset = threatPosition - position;
        Vector3 relativeVelocity = threatVelocity - velocity;
        float closestTime = relativeVelocity.sqrMagnitude > 0.001f
            ? Mathf.Clamp(-Vector3.Dot(offset, relativeVelocity) / relativeVelocity.sqrMagnitude, 0f, horizon)
            : 0f;
        float distance = (offset + relativeVelocity * closestTime).magnitude;
        return Mathf.Clamp01(1f - distance / Mathf.Max(0.01f, radius));
    }

    public static float SafeRadius(float current, float next, float remainingSeconds, float speed)
    {
        // Start moving inward while there is still time to cross the closing ring.
        float travelBudget = Mathf.Max(0f, remainingSeconds - 3f) * Mathf.Max(1f, speed);
        return Mathf.Max(5f, Mathf.Min(current - 5f, next - 5f + travelBudget));
    }

    public static bool ShouldUseVoid(float ownHealth, float enemyHealth, bool hasOpponent)
    {
        return hasOpponent && ownHealth > 0f && enemyHealth > 0f &&
            VoidAbility.CanActivateForMode(false, true, ownHealth, enemyHealth);
    }
}
