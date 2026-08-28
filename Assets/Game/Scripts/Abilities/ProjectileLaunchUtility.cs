using UnityEngine;

/// <summary>
/// Creates a thrown ability completely outside its owner's colliders. The
/// clearance is calculated from the real player and projectile bounds, so it
/// continues to work if either model is resized later.
/// </summary>
public static class ProjectileLaunchUtility
{
    private const float MinimumClearance = 0.2f;

    public static GameObject InstantiateSafely(GameObject prefab, Transform owner,
        Vector3 fallbackPosition, Vector3 direction,
        float minimumCenterHeightAboveOwner = 0f,
        bool useHorizontalPlacement = false)
    {
        if (prefab == null)
            return null;

        direction = NormalizeDirection(direction, owner);
        Vector3 placementDirection = useHorizontalPlacement
            ? HorizontalDirection(direction, owner)
            : direction;
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

        if (owner == null)
            return Object.Instantiate(prefab, fallbackPosition, rotation);

        if (!TryGetSolidBounds(owner, out Bounds ownerBounds))
        {
            fallbackPosition.y = Mathf.Max(
                fallbackPosition.y,
                owner.position.y + Mathf.Max(0f, minimumCenterHeightAboveOwner));
            return Object.Instantiate(prefab, fallbackPosition, rotation);
        }

        // Instantiate well clear of the player first. Awake can safely install
        // collision ignores before the projectile is moved to its exact launch
        // point and registered with the network.
        float provisionalDistance = ownerBounds.extents.magnitude + 3f;
        Vector3 provisionalPosition = ownerBounds.center + placementDirection * provisionalDistance;
        GameObject projectile = Object.Instantiate(prefab, provisionalPosition, rotation);

        if (TryGetSolidBounds(projectile.transform, out Bounds projectileBounds))
        {
            float ownerExtent = ProjectedExtent(ownerBounds, placementDirection);
            float projectileExtent = ProjectedExtent(projectileBounds, placementDirection);
            Vector3 desiredProjectileCenter = ownerBounds.center + placementDirection *
                (ownerExtent + projectileExtent + MinimumClearance);
            desiredProjectileCenter = ElevatedLaunchCenter(
                ownerBounds, desiredProjectileCenter, minimumCenterHeightAboveOwner);
            projectile.transform.position += desiredProjectileCenter - projectileBounds.center;
        }
        else
        {
            projectile.transform.position = ownerBounds.center + placementDirection *
                (ownerBounds.extents.magnitude + MinimumClearance);
            projectile.transform.position = ElevatedLaunchCenter(
                ownerBounds, projectile.transform.position, minimumCenterHeightAboveOwner);
        }

        IgnoreOwnerCollision(projectile, owner);
        return projectile;
    }

    public static Vector3 ElevatedLaunchCenter(
        Bounds ownerBounds,
        Vector3 candidateCenter,
        float minimumCenterHeightAboveOwner)
    {
        candidateCenter.y = Mathf.Max(
            candidateCenter.y,
            ownerBounds.center.y + Mathf.Max(0f, minimumCenterHeightAboveOwner));
        return candidateCenter;
    }

    private static Vector3 HorizontalDirection(Vector3 direction, Transform owner)
    {
        Vector3 horizontal = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (horizontal.sqrMagnitude < 0.0001f && owner != null)
            horizontal = Vector3.ProjectOnPlane(owner.forward, Vector3.up);
        return horizontal.sqrMagnitude > 0.0001f ? horizontal.normalized : Vector3.forward;
    }

    private static Vector3 NormalizeDirection(Vector3 direction, Transform owner)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = owner != null ? owner.forward : Vector3.forward;
        return direction.normalized;
    }

    private static bool TryGetSolidBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        bool found = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger ||
                !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!found)
            {
                bounds = collider.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return found;
    }

    private static float ProjectedExtent(Bounds bounds, Vector3 direction)
    {
        Vector3 extent = bounds.extents;
        return Mathf.Abs(direction.x) * extent.x +
               Mathf.Abs(direction.y) * extent.y +
               Mathf.Abs(direction.z) * extent.z;
    }

    private static void IgnoreOwnerCollision(GameObject projectile, Transform owner)
    {
        Collider[] projectileColliders = projectile.GetComponentsInChildren<Collider>(true);
        Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < projectileColliders.Length; i++)
        {
            Collider projectileCollider = projectileColliders[i];
            if (projectileCollider == null || projectileCollider.isTrigger)
                continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider ownerCollider = ownerColliders[j];
                if (ownerCollider != null)
                    Physics.IgnoreCollision(projectileCollider, ownerCollider, true);
            }
        }
    }
}
