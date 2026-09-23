using NUnit.Framework;
using UnityEngine;

public sealed class BlackHoleProjectileTests
{
    [Test]
    public void BlackHoleGravityPolicyIsDedicatedFromBlackCubePolicy()
    {
        string source = System.IO.File.ReadAllText(
            "Assets/Game/Scripts/Abilities/NetworkProjectilePhysics.cs");

        StringAssert.Contains("gravityFreeProjectile = GetComponentInChildren<BlackHoleKill>(true) != null;", source);
        StringAssert.Contains("body.useGravity = !gravityFreeProjectile;", source);
    }
}
