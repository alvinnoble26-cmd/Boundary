using UnityEngine;

public class BlackKill : MonoBehaviour
{
    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered)
            return;

        if (GameManager.I == null)
            return;

        PlayerMovement hitPm = other.GetComponentInParent<PlayerMovement>();

        if (hitPm == null)
            return;

        // Every client simulates trigger contacts for replicated players. Only
        // the client that owns the player that was hit may report that loss.
        if (!hitPm.isOwner)
            return;

        hasTriggered = true;

        Debug.Log("[BlackKill] Locally owned player touched arena black hole.");
        SfxManager.PlayLethalHit();
        GameManager.I.ReportLocalPlayerLost("You were consumed by the black hole.");
    }
}
