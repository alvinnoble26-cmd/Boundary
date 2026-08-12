using UnityEngine;

public class ThrowableStick : MonoBehaviour
{
    [SerializeField] private float destroyAfter = 5f;
    [SerializeField] private bool freezeOnFirstHit = true;

    private Rigidbody rb;
    private bool stuck;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision col)
    {
        if (stuck) return;

        // Optional: only stick to ground-like objects:
        // if (!col.collider.CompareTag("Ground")) return;

        stuck = true;

        if (freezeOnFirstHit && rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.isKinematic = true; // easiest “stick”
            // OR if you prefer physics still active:
            // rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        Destroy(gameObject, destroyAfter);
    }
}
