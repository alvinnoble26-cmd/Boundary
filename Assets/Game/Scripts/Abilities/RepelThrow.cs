using UnityEngine;
using PurrNet;

public class RepelThrow : MonoBehaviour, IAbility
{
    public AbilityId Id => AbilityId.RepelThrow;
    public float CooldownDuration => cooldown;
    public const float BuildUpSeconds = 0.5f;
    public const float PreviousEffectRadius = 220f;
    public const float EffectRadiusMultiplier = 0.6f;
    public const float EffectRadius = PreviousEffectRadius * EffectRadiusMultiplier;

    [Header("Repel Field Prefab (spawned on the caster)")]
    [SerializeField] private GameObject objectToThrow;

    [Header("Timing")]
    [SerializeField] private float cooldown = 3f;

    [Header("Repulsion Field Tuning")]
    [SerializeField, Min(0f), Tooltip("Raw push force. Heavier objects receive less movement.")]
    private float repulsionForce = 220f;
    [SerializeField, Min(0f), Tooltip("Maximum response caused by the repulsion pulse.")]
    private float fieldAcceleration = 88f;


    private float nextReadyTime;

    public void Activate()
    {
        if (Time.time < nextReadyTime)
            return;

        if (objectToThrow == null)
        {
            Debug.LogError($"[{name}] RepelThrow FAILED. prefab=NULL");
            return;
        }

        nextReadyTime = Time.time + cooldown;
        ThrowOnce();
    }

    public void ActivateFromNetwork(Vector3 spawnPos, Vector3 dir)
    {
        if (Time.time < nextReadyTime)
            return;

        nextReadyTime = Time.time + cooldown;
        ThrowOnce();
    }

    // Repel no longer throws a ball: the field is spawned on the caster and
    // follows them, pushing everything around them away when it pulses.
    private void ThrowOnce()
    {
        PlayerMovement ownerPm = GetComponentInParent<PlayerMovement>();
        Transform owner = ownerPm != null ? ownerPm.transform : transform.root;
        Vector3 center = owner.position + Vector3.up * PlayerMovement.StandingCenterHeight;

        GameObject field = Instantiate(objectToThrow, center, Quaternion.identity);
        if (field == null)
            return;

        ForceField forceField = field.GetComponentInChildren<ForceField>();
        if (forceField != null)
        {
            forceField.ConfigureRepel(repulsionForce, fieldAcceleration, EffectRadius,
                BuildUpSeconds);
            forceField.AttachToCaster(owner, owner.GetComponent<Rigidbody>());
        }

        // The caster is immune to all damage while the red field builds up.
        BoundaryPlayerState casterState = owner.GetComponent<BoundaryPlayerState>();
        if (casterState != null)
            casterState.ServerGrantInvulnerability(BuildUpSeconds);

        NetworkIdentity.Spawn(field, objectToThrow);
        Debug.Log("[RepelThrow] Repel field activated on caster " + owner.name);
    }

}
