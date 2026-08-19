using PurrNet;
using UnityEngine;

/// <summary>
/// Makes black-hole-textured arena cubes lethal to the local player while also
/// letting the server consume networked movable arena cubes.
/// </summary>
public class BlackCubeKill : MonoBehaviour
{
    private bool hasKilledLocalPlayer;

    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision != null)
            HandleContact(collision.collider);
    }

    private void HandleContact(Collider other)
    {
        if (other == null)
            return;

        PlayerMovement hitPlayer = other.GetComponentInParent<PlayerMovement>();
        if (hitPlayer != null)
        {
            KillLocalPlayer(hitPlayer);
            return;
        }

        TryConsumeMovableCube(other);
    }

    private void KillLocalPlayer(PlayerMovement hitPlayer)
    {
        // Every peer has a physics copy of the arena. Only the client that owns
        // the contacted player may report that player's loss.
        if (hasKilledLocalPlayer || hitPlayer == null || !hitPlayer.isOwner || GameManager.I == null)
            return;

        hasKilledLocalPlayer = true;

        Debug.Log("[BlackCubeKill] Local player touched a black-hole cube.");
        LocalLethalFeedback.VibrateForAcceptedLocalContact();
        SfxManager.PlayLethalHit();
        GameManager.I.ReportLocalPlayerLost("You were consumed by the black hole.");
    }

    private void TryConsumeMovableCube(Collider other)
    {
        NetworkManager net = NetworkManager.main;
        if (net == null || !net.isServer)
            return;

        Rigidbody body = other.attachedRigidbody;

        if (body == null || body.isKinematic)
            return;

        GameObject target = body.gameObject;

        if (!target.CompareTag("Wall"))
            return;

        // Black holes can also have rigidbodies and must not be mistaken for a
        // movable arena cube.
        if (target.GetComponent<BlackKill>() != null ||
            target.GetComponent<BlackHoleKill>() != null)
        {
            return;
        }

        NetworkIdentity identity = target.GetComponent<NetworkIdentity>();
        if (identity == null)
        {
            Debug.LogWarning("[BlackCubeKill] Refusing a local-only destroy for unnetworked cube: " + target.name);
            return;
        }

        Debug.Log("[BlackCubeKill] Server despawning consumed cube: " + target.name);
        identity.Despawn();
    }
}
