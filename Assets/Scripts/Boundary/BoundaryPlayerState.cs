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

    [Header("Void")]
    [SerializeField, Min(1f)] private float voidKillDepthBelowArena = 4f;

    private readonly SyncVar<BoundaryKnockoutState> state = new(BoundaryKnockoutState.Grounded, ownerAuth: true);

    private PlayerMovement movement;
    private float horizonEnteredAt = -1f;
    private float outOfBoundsEnteredAt = -1f;
    private bool reportedLoss;

    public BoundaryKnockoutState State => state.value;
    public float EscapeProgress
    {
        get
        {
            float enteredAt = state.value == BoundaryKnockoutState.EventHorizon
                ? horizonEnteredAt
                : state.value == BoundaryKnockoutState.OutOfBounds ? outOfBoundsEnteredAt : -1f;
            return enteredAt >= 0f
                ? Mathf.Clamp01((Time.time - enteredAt) / escapeWindowSeconds)
                : 0f;
        }
    }

    public bool IsOutOfBounds => state.value == BoundaryKnockoutState.OutOfBounds;

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
        if (match == null)
            return;

        if (BoundaryMath.IsBelowVoidKillPlane(
                transform.position.y,
                match.ArenaFloorY,
                voidKillDepthBelowArena))
        {
            ConsumePlayer("You fell into the void.");
            return;
        }

        if (match.Phase == BoundaryPhase.Waiting)
        {
            SetState(movement.IsGrounded ? BoundaryKnockoutState.Grounded : BoundaryKnockoutState.Airborne);
            return;
        }

        float horizontalDistance = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(match.ArenaCenter.x, match.ArenaCenter.z));
        float outOfBoundsRadius = match.RingRadius + BoundaryMath.OutOfBoundsMargin(match.Phase);
        if (horizontalDistance > outOfBoundsRadius)
        {
            if (state.value != BoundaryKnockoutState.OutOfBounds &&
                state.value != BoundaryKnockoutState.Consumed)
            {
                outOfBoundsEnteredAt = Time.time;
                SetState(BoundaryKnockoutState.OutOfBounds);
            }
            else if (state.value == BoundaryKnockoutState.OutOfBounds &&
                     Time.time - outOfBoundsEnteredAt >= escapeWindowSeconds)
            {
                ConsumePlayer("You went out of bounds.");
            }
            return;
        }

        if (state.value == BoundaryKnockoutState.OutOfBounds &&
            horizontalDistance > match.RingRadius - 1f)
            return;

        outOfBoundsEnteredAt = -1f;

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
                ConsumePlayer("You crossed the event horizon.");
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

        movement.ApplyAbilityImpulse(velocityChange);
    }

    private void SetState(BoundaryKnockoutState next)
    {
        if (state.value != next)
            state.value = next;
    }

    public void ConsumeFromHazard(string reason)
    {
        if (!isOwner)
            return;

        ConsumePlayer(string.IsNullOrWhiteSpace(reason)
            ? "You were consumed by the black hole."
            : reason);
    }

    private void ConsumePlayer(string reason)
    {
        if (reportedLoss)
            return;

        reportedLoss = true;
        SetState(BoundaryKnockoutState.Consumed);
        LocalLethalFeedback.VibrateForAcceptedLocalContact();
        SfxManager.PlayLethalHit();
        if (GameManager.I != null)
            GameManager.I.ReportLocalPlayerLost(reason);
    }
}
