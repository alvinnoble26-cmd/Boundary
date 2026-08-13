using UnityEngine;
using PurrNet;

public class BlackThrow : MonoBehaviour, IAbility
{
    public AbilityId Id => AbilityId.BlackThrow;
    public float CooldownDuration => throwCooldown;

    [Header("References")]
    [SerializeField] private Transform aimTransform;      
    [SerializeField] private Transform attackPoint;      
    [SerializeField] private GameObject objectToThrow;    

    [Header("Settings")]
    [SerializeField] private int totalThrows = 5;
    [SerializeField] private float throwCooldown = 3.5f;

    [Header("Throwing Force")]
    [SerializeField] private float throwForce = 20f;
    [SerializeField] private float throwUpwardForce = 5f; 

    private bool readyToThrow = true;

    public void Activate()
    {
        if (!readyToThrow) return;
        if (totalThrows <= 0) return;
        if (aimTransform == null || attackPoint == null || objectToThrow == null)
        {
            Debug.LogWarning("BlackThrow missing refs (aimTransform/attackPoint/objectToThrow).");
            return;
        }

        Throw();
    }

    private void Throw()
    {
        Vector3 dir = aimTransform.forward;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        Throw(attackPoint.position, dir);
    }

    public void ActivateFromNetwork(Vector3 spawnPosition, Vector3 direction)
    {
        if (!readyToThrow || totalThrows <= 0 || objectToThrow == null)
            return;

        Throw(spawnPosition, direction);
    }

    private void Throw(Vector3 spawnPosition, Vector3 direction)
    {
        readyToThrow = false;

        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;
        direction.Normalize();

        var ownerPm = GetComponentInParent<PlayerMovement>();
        Transform owner = ownerPm != null ? ownerPm.transform : transform.root;
        GameObject projectile = ProjectileLaunchUtility.InstantiateSafely(
            objectToThrow, owner, spawnPosition, direction);
        if (projectile == null)
        {
            readyToThrow = true;
            return;
        }

        NetworkIdentity.Spawn(projectile, objectToThrow);

        var kill = projectile.GetComponentInChildren<BlackHoleKill>();
        if (kill != null && ownerPm != null)
            kill.Init(ownerPm, 0.75f);


  
        Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
        if (projectileRb == null)
        {
            Debug.LogWarning("Thrown object has no Rigidbody.");
            Destroy(projectile);
            readyToThrow = true;
            return;
        }

        Vector3 forceToAdd = direction * throwForce + Vector3.up * throwUpwardForce;

        // Optional: inherit player velocity so throws feel consistent while moving
        // projectileRb.linearVelocity = GetComponentInParent<Rigidbody>()?.linearVelocity ?? Vector3.zero;

        projectileRb.AddForce(forceToAdd, ForceMode.Impulse);

        totalThrows--;

        Invoke(nameof(ResetThrow), throwCooldown);
    }

    private void ResetThrow()
    {
        readyToThrow = true;
    }
}
