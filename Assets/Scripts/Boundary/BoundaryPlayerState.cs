using PurrNet;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public sealed class BoundaryPlayerState : NetworkBehaviour
{
    [Header("Event horizon")]
    [SerializeField, Min(2f)] private float horizonDistanceBelowCore = 5.5f;
    [SerializeField, Min(0.5f)] private float escapeWindowSeconds = 1.6f;
    [SerializeField, Min(1f)] private float horizonHorizontalRadius = 10f;

    private readonly SyncVar<BoundaryKnockoutState> state = new(BoundaryKnockoutState.Grounded, ownerAuth: true);

    private PlayerMovement movement;
    private float horizonEnteredAt = -1f;
    private bool reportedLoss;

    public BoundaryKnockoutState State => state.value;
    public float EscapeProgress => state.value == BoundaryKnockoutState.EventHorizon && horizonEnteredAt >= 0f
        ? Mathf.Clamp01((Time.time - horizonEnteredAt) / escapeWindowSeconds)
        : 0f;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
    }

    private void FixedUpdate()
    {
        if (!isOwner || movement == null)
            return;

        if (state.value == BoundaryKnockoutState.Consumed)
            return;

        BoundaryMatchController match = BoundaryMatchController.Instance;
        if (match == null || match.Phase == BoundaryPhase.Waiting)
        {
            SetState(movement.IsGrounded ? BoundaryKnockoutState.Grounded : BoundaryKnockoutState.Airborne);
            return;
        }

        float horizonY = match.SingularityPosition.y - horizonDistanceBelowCore;
        Vector3 flatOffset = transform.position - match.ArenaCenter;
        flatOffset.y = 0f;
        bool insideHorizonColumn = flatOffset.magnitude <= horizonHorizontalRadius;
        bool beyondHorizon = transform.position.y >= horizonY && insideHorizonColumn;

        if (beyondHorizon)
        {
            if (state.value != BoundaryKnockoutState.EventHorizon &&
                state.value != BoundaryKnockoutState.Consumed)
            {
                horizonEnteredAt = Time.time;
                SetState(BoundaryKnockoutState.EventHorizon);
            }
            else if (state.value == BoundaryKnockoutState.EventHorizon &&
                     Time.time - horizonEnteredAt >= escapeWindowSeconds)
            {
                ConsumePlayer();
            }
            return;
        }

        // A full meter of separation prevents boundary jitter from repeatedly
        // entering and leaving the final escape window.
        if (state.value == BoundaryKnockoutState.EventHorizon && transform.position.y > horizonY - 1f)
            return;

        horizonEnteredAt = -1f;
        SetState(movement.IsStableGrounded
            ? BoundaryKnockoutState.Grounded
            : BoundaryKnockoutState.Airborne);
    }

    public void ServerPushOwner(Vector3 velocityChange)
    {
        if (!isServer || !owner.HasValue)
            return;

        PushOwner(owner.Value, velocityChange);
    }

    [TargetRpc]
    private void PushOwner(PlayerID target, Vector3 velocityChange)
    {
        if (!isOwner || movement == null)
            return;

        movement.ApplyBoundaryImpulse(velocityChange);
    }

    private void SetState(BoundaryKnockoutState next)
    {
        if (state.value != next)
            state.value = next;
    }

    private void ConsumePlayer()
    {
        if (reportedLoss)
            return;

        reportedLoss = true;
        SetState(BoundaryKnockoutState.Consumed);
        SfxManager.PlayLethalHit();
        if (GameManager.I != null)
            GameManager.I.ReportLocalPlayerLost("You crossed the event horizon.");
    }
}
