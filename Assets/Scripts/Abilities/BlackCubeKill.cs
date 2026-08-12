using UnityEngine;

/// <summary>
/// Shared black-hole handling for movable arena cubes. Static arena walls use
/// the same Wall tag, so only a non-kinematic Rigidbody is eligible.
/// </summary>
public class BlackCubeKill : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        TryKill(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision != null)
            TryKill(collision.collider);
    }

    private void TryKill(Collider other)
    {
        if (other == null)
            return;

        Rigidbody body = other.attachedRigidbody;

        if (body == null || body.isKinematic)
            return;

        GameObject target = body.gameObject;

        if (!target.CompareTag("Wall"))
            return;

        // Players and black holes can also have rigidbodies; neither should be
        // mistaken for a movable arena cube.
        if (target.GetComponentInParent<PlayerMovement>() != null ||
            target.GetComponent<BlackKill>() != null ||
            target.GetComponent<BlackHoleKill>() != null)
        {
            return;
        }

        Debug.Log("[BlackCubeKill] Cube consumed: " + target.name);
        Object.Destroy(target);
    }
}
