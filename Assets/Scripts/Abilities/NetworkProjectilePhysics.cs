using System.Collections;
using PurrNet;
using UnityEngine;

/// <summary>
/// Projectile motion is simulated only by the dedicated server. Clients keep
/// the replicated Rigidbody kinematic while NetworkTransform applies the
/// server's position, but projectile trigger/force scripts can still run.
/// </summary>
public class NetworkProjectilePhysics : MonoBehaviour
{
    private Rigidbody body;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
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
