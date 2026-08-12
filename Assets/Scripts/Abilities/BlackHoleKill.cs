using UnityEngine;

public class BlackHoleKill : MonoBehaviour
{
    [Header("Owner Immunity")]
    [SerializeField] private float ownerImmunitySeconds = 0.75f;

    [Header("Lifetime")]
    [SerializeField] private float lifetimeSeconds = 5f;

    private PlayerMovement ownerPm;
    private float armedTime;
    private float destroyTime;
    private bool hasTriggered;

    /// <summary>
    /// Call this right after spawning the black hole.
    /// This resets immunity and lifetime every time.
    /// </summary>
    public void Init(PlayerMovement owner, float immunitySeconds = 0.75f)
    {
        ownerPm = owner;
        ownerImmunitySeconds = immunitySeconds;

        armedTime = Time.time;
        destroyTime = Time.time + lifetimeSeconds;
        hasTriggered = false;

        Debug.Log("[BlackHoleKill] Init owner=" + (ownerPm != null ? ownerPm.name : "NULL"));
    }

    private void Start()
    {
        // Every client receives the same networked projectile, making its spawn
        // sound universal without changing the multiplayer RPC layout.
        SfxManager.PlayBlackHoleThrow();

        if (armedTime <= 0f)
            armedTime = Time.time;

        if (destroyTime <= 0f)
            destroyTime = Time.time + lifetimeSeconds;
    }

    private void Update()
    {
        if (Time.time >= destroyTime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered)
            return;

        if (GameManager.I == null)
            return;

        PlayerMovement hitPm = other.GetComponentInParent<PlayerMovement>();

        if (hitPm == null)
            return;

        // Trigger callbacks also run for remote player replicas. The owner of
        // the player that was hit is the only client allowed to report a loss.
        if (!hitPm.isOwner)
            return;

        // Give the owner a short grace period so the projectile does not instantly kill them.
        // Network replicas do not carry the server's direct ownerPm reference,
        // so arm every replica after the same short launch grace period.
        if (Time.time - armedTime < ownerImmunitySeconds)
            return;

        hasTriggered = true;

        Debug.Log("[BlackHoleKill] Locally owned player touched thrown black hole.");
        SfxManager.PlayLethalHit();

        GameManager.I.ReportLocalPlayerLost("You were consumed by the black hole.");

        Destroy(gameObject);
    }
}
