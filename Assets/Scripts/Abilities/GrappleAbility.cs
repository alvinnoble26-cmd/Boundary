using UnityEngine;

public sealed class GrappleAbility : MonoBehaviour, IAbility
{
    public AbilityId Id => AbilityId.Grapple;
    public float CooldownDuration => 3f;
    public const float CooldownSeconds = 3f;
    public const float MaximumRange = 150f;
    private const float PullAcceleration = 240f;

    private PlayerMovement movement;
    private LineRenderer rope;
    private bool active;
    private bool movable;
    private Vector3 anchor;
    private Transform target;
    private float ropeStartTime;
    private Cam localCamera;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
    }

    private void FixedUpdate()
    {
        if (!active || movable || movement == null || !movement.isOwner || movement.rb == null)
            return;

        Vector3 delta = anchor - movement.rb.position;
        if (delta.magnitude <= 1.5f)
            return;

        movement.SetMovementSuppressed(true, 80f, false);
        movement.rb.AddForce(delta.normalized * PullAcceleration, ForceMode.Acceleration);
    }

    private void LateUpdate()
    {
        if (!active || rope == null)
            return;
        Vector3 start = localCamera != null && movement != null && movement.isOwner
            ? localCamera.GetGrappleArmOrigin()
            : transform.position + Vector3.up * 1.1f;
        Vector3 end = target != null ? target.position : anchor;
        for (int index = 0; index < rope.positionCount; index++)
        {
            float t = index / (float)(rope.positionCount - 1);
            rope.SetPosition(index, Vector3.Lerp(start, end, t));
        }
    }

    public void Activate() { }

    public void BeginPresentation(Vector3 hitPoint, Transform liveTarget, bool pullsTarget)
    {
        anchor = hitPoint;
        target = liveTarget;
        movable = pullsTarget;
        active = true;
        ropeStartTime = Time.time;
        if (movement != null && movement.isOwner)
        {
            localCamera = GetComponentInChildren<Cam>(true);
            localCamera?.SetGrappleArmActive(true, hitPoint - transform.position);
        }
        if (rope == null)
        {
            rope = gameObject.AddComponent<LineRenderer>();
            rope.positionCount = 9;
            rope.startWidth = rope.endWidth = 0.1f;
            rope.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            rope.material.SetColor("_BaseColor", Color.black);
            rope.startColor = Color.black;
            rope.endColor = Color.black;
        }
        rope.enabled = true;
    }

    public void EndPresentation()
    {
        active = false;
        if (movement != null && movement.isOwner)
        {
            movement.ReleaseMovementSuppressionPreservingMomentum();
            localCamera?.SetGrappleArmActive(false, Vector3.forward);
        }
        if (rope != null)
            rope.enabled = false;
    }

    public void CancelForJump()
    {
        if (active && !movable && movement != null && movement.isOwner && movement.rb != null)
        {
            Vector3 delta = anchor - movement.rb.position;
            if (delta.sqrMagnitude > 1.5f * 1.5f)
            {
                // Preserve the pull that would have been applied on this
                // physics tick as a one-shot launch impulse before ending it.
                movement.rb.AddForce(
                    delta.normalized * (PullAcceleration * Time.fixedDeltaTime * movement.rb.mass),
                    ForceMode.Impulse);
            }
        }

        EndPresentation();
    }
}
