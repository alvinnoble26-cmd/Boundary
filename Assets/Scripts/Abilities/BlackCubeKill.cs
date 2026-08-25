using PurrNet;
using UnityEngine;

/// <summary>
/// Makes black-hole-textured arena cubes lethal to the local player while also
/// letting the server consume networked movable arena cubes.
/// </summary>
public class BlackCubeKill : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision != null)
            HandleContact(collision.collider);
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterServerPlayerContact(other);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision != null)
            RegisterServerPlayerContact(collision.collider);
    }

    private void HandleContact(Collider other)
    {
        if (other == null)
            return;

        PlayerMovement hitPlayer = other.GetComponentInParent<PlayerMovement>();
        if (hitPlayer != null)
        {
            RegisterServerPlayerContact(other);
            return;
        }

        TryConsumeMovableCube(other);
    }

    private void RegisterServerPlayerContact(Collider other)
    {
        NetworkManager net = NetworkManager.main;
        if (net == null || !net.isServer || other == null)
            return;

        BoundaryPlayerState state = other.GetComponentInParent<BoundaryPlayerState>();
        if (state != null)
            state.ServerRegisterBlackHoleContact(gameObject.GetInstanceID());
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
