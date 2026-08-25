using System.Collections;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;

/// <summary>
/// Projectile motion is simulated only by the dedicated server. Clients keep
/// the replicated Rigidbody kinematic while NetworkTransform applies the
/// server's position, but projectile trigger/force scripts can still run.
/// </summary>
public class NetworkProjectilePhysics : MonoBehaviour
{
    private static readonly List<NetworkProjectilePhysics> ActiveProjectiles =
        new List<NetworkProjectilePhysics>();
    private Rigidbody body;
    private bool blackHoleProjectile;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        blackHoleProjectile = GetComponentInChildren<BlackHoleKill>(true) != null ||
                              GetComponentInChildren<BlackCubeKill>(true) != null;
        if (body != null)
        {
            // Network-spawned client copies must never get a speculative local
            // gravity step. Server-spawned copies are enabled immediately.
            bool server = NetworkManager.main != null && NetworkManager.main.isServer;
            SetServerSimulation(server);
        }

        // The root collider gives the projectile world collision, but it must
        // not physically shove a player Rigidbody. BlackHoleKill uses a
        // separate child trigger, so ignoring only these solid-collider pairs
        // preserves elimination detection.
        Collider[] projectileColliders = GetComponents<Collider>();
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Collider projectileCollider in projectileColliders)
        {
            if (projectileCollider == null || projectileCollider.isTrigger)
                continue;

            foreach (PlayerMovement player in players)
            {
                if (player == null)
                    continue;

                Collider[] playerColliders = player.GetComponentsInChildren<Collider>(true);
                foreach (Collider playerCollider in playerColliders)
                {
                    if (playerCollider != null)
                        Physics.IgnoreCollision(projectileCollider, playerCollider, true);
                }
            }
        }
    }

    private void OnEnable()
    {
        if (!ActiveProjectiles.Contains(this))
            ActiveProjectiles.Add(this);
    }

    private void OnDisable()
    {
        ActiveProjectiles.Remove(this);
    }

    public static int ServerApplyBlackHoleGravity(Vector3 center, float radius, float acceleration)
    {
        NetworkManager net = NetworkManager.main;
        if (net == null || !net.isServer || radius <= 0f || acceleration <= 0f)
            return 0;

        int affected = 0;
        for (int index = ActiveProjectiles.Count - 1; index >= 0; index--)
        {
            NetworkProjectilePhysics projectile = ActiveProjectiles[index];
            if (projectile == null)
            {
                ActiveProjectiles.RemoveAt(index);
                continue;
            }
            if (!projectile.blackHoleProjectile || projectile.body == null)
                continue;

            Vector3 delta = center - projectile.body.worldCenterOfMass;
            float distance = delta.magnitude;
            if (distance <= 0.05f || distance >= radius)
                continue;

            projectile.SetServerSimulation(true);
            projectile.body.WakeUp();
            projectile.body.AddForce(delta.normalized * acceleration *
                Mathf.Clamp01(1f - distance / radius), ForceMode.Acceleration);
            affected++;
        }

        return affected;
    }

    private IEnumerator Start()
    {
        NetworkManager net = null;
        while (net == null)
        {
            net = NetworkManager.main;
            yield return null;
        }

        if (net.isServer)
        {
            SetServerSimulation(true);
            yield break;
        }

        if (body == null)
            yield break;

        SetServerSimulation(false);
    }

    public void PrepareForServerLaunch()
    {
        NetworkManager net = NetworkManager.main;
        if (net == null || !net.isServer)
            return;

        SetServerSimulation(true);
    }

    private void SetServerSimulation(bool simulate)
    {
        if (body == null)
            return;

        if (!simulate)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        body.isKinematic = !simulate;
        body.collisionDetectionMode = simulate
            ? CollisionDetectionMode.ContinuousDynamic
            : CollisionDetectionMode.ContinuousSpeculative;
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }
}
