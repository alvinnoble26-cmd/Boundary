using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BoundaryCpuTests
{
    [Test]
    public void RandomLoadoutsAlwaysContainThreeDistinctEnabledAbilities()
    {
        HashSet<AbilityId> coverage = new HashSet<AbilityId>();
        for (int seed = 0; seed < 500; seed++)
        {
            AbilityId[] loadout = BoundaryCpuRules.RandomLoadout(seed);
            Assert.AreEqual(3, loadout.Length);
            Assert.AreEqual(3, new HashSet<AbilityId>(loadout).Count);
            foreach (AbilityId id in loadout)
            {
                Assert.IsTrue(LoadoutManager.IsAbilityEnabled(id));
                coverage.Add(id);
            }
        }
        foreach (AbilityId id in Enum.GetValues(typeof(AbilityId)))
            Assert.AreEqual(LoadoutManager.IsAbilityEnabled(id), coverage.Contains(id), id.ToString());
    }

    [Test]
    public void SeedReproducesLoadout()
    {
        CollectionAssert.AreEqual(BoundaryCpuRules.RandomLoadout(42), BoundaryCpuRules.RandomLoadout(42));
    }

    [Test]
    public void LoadoutMaskIgnoresSlotOrder()
    {
        AbilityId[] first = { AbilityId.Dash, AbilityId.Teleport, AbilityId.Slice };
        AbilityId[] second = { AbilityId.Slice, AbilityId.Dash, AbilityId.Teleport };
        Assert.AreEqual(BoundaryCpuRules.LoadoutMask(first), BoundaryCpuRules.LoadoutMask(second));
    }

    [Test]
    public void AimInterceptsCrossingTarget()
    {
        Vector3 target = new Vector3(0f, 0f, 30f);
        Vector3 velocity = Vector3.right * 7f;
        Vector3 aim = BoundaryCpuRules.LeadTarget(Vector3.zero, target, velocity, 40f);
        float flightTime = aim.magnitude / 40f;
        Assert.Less(Vector3.Distance(aim, target + velocity * flightTime), 0.01f);
    }

    [Test]
    public void AimAccountsForChargeWindup()
    {
        Vector3 target = new Vector3(0f, 0f, 20f);
        Vector3 velocity = Vector3.right * 7f;
        Vector3 aim = BoundaryCpuRules.LeadTarget(Vector3.zero, target, velocity,
            ChargeAbility.ProjectileSpeed, ChargeAbility.ChargeSeconds);
        Assert.Less(Vector3.Distance(aim, target + velocity *
            (ChargeAbility.ChargeSeconds + aim.magnitude / ChargeAbility.ProjectileSpeed)), 0.01f);
    }

    [Test]
    public void ImpossibleInterceptStillProducesFiniteBoundedAim()
    {
        Vector3 aim = BoundaryCpuRules.LeadTarget(Vector3.zero, Vector3.forward * 10f,
            Vector3.forward * 70f, 20f);
        Assert.IsFalse(float.IsNaN(aim.z));
        Assert.LessOrEqual(aim.magnitude, 150f);
    }

    [Test]
    public void DodgeDirectionReducesIncomingProjectileDanger()
    {
        float standing = BoundaryCpuRules.MovingThreat(Vector3.zero, Vector3.zero,
            Vector3.forward * 10f, Vector3.back * 20f, 2f, 1f);
        float dodging = BoundaryCpuRules.MovingThreat(Vector3.zero, Vector3.right * 7f,
            Vector3.forward * 10f, Vector3.back * 20f, 2f, 1f);
        Assert.Greater(standing, 0.9f);
        Assert.Less(dodging, standing);
        Assert.AreEqual(0f, BoundaryCpuRules.MovingThreat(Vector3.zero, Vector3.zero,
            Vector3.forward * 10f, Vector3.forward * 20f, 2f, 1f));
    }

    [Test]
    public void RetreatBeginsBeforeRingCollapse()
    {
        Assert.AreEqual(101f, BoundaryCpuRules.SafeRadius(106f, 68f, 30f, 7f));
        Assert.Less(BoundaryCpuRules.SafeRadius(106f, 68f, 6f, 7f), 101f);
        Assert.AreEqual(63f, BoundaryCpuRules.SafeRadius(106f, 68f, 0f, 7f));
    }

    [Test]
    public void CpuVoidUsesCompetitiveHealthRule()
    {
        Assert.IsFalse(BoundaryCpuRules.ShouldUseVoid(100f, 100f, true));
        Assert.IsFalse(BoundaryCpuRules.ShouldUseVoid(50f, 60f, true));
        Assert.IsFalse(BoundaryCpuRules.ShouldUseVoid(100f, 50f, false));
        Assert.IsFalse(BoundaryCpuRules.ShouldUseVoid(100f, 0f, true));
        Assert.IsTrue(BoundaryCpuRules.ShouldUseVoid(61f, 60f, true));
    }

    [Test]
    public void CpuUsesBaseForRecoveryAndMissingFloorButNotOnSafeGround()
    {
        Assert.IsTrue(BoundaryCpuRules.ShouldUseBase(true, true, true));
        Assert.IsTrue(BoundaryCpuRules.ShouldUseBase(false, false, true));
        Assert.IsTrue(BoundaryCpuRules.ShouldUseBase(false, true, false));
        Assert.IsFalse(BoundaryCpuRules.ShouldUseBase(false, true, true));
    }

    [Test]
    public void CpuMarkerAloneDoesNotGrantAuthorityWithoutServer()
    {
        GameObject player = new GameObject("CPU authority test");
        try
        {
            PlayerMovement movement = player.AddComponent<PlayerMovement>();
            movement.ConfigureCpuControl();
            Assert.IsTrue(movement.IsCpuControlled);
            Assert.IsFalse(movement.HasSimulationAuthority);
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }
}
