using UnityEngine;

// Runtime-only local camera presentation. This is deliberately not a networked
// object and does not need a prefab reference, keeping it out of third-person
// skin synchronization and Player.prefab serialization.
public sealed class FirstPersonArmPresentation : MonoBehaviour
{
    private const float ThrowDuration = 0.16f;
    private const float TeleportDuration = 0.5f;

    private Transform arm;
    private Renderer armRenderer;
    private float throwEndsAt;
    private float teleportStartedAt = -1f;
    private bool movementActive;
    private bool grappleActive;
    private string skinId = "beard";

    public void SetSkin(string value)
    {
        skinId = value == "turtle" || value == "sun_ducker" ? value : "beard";
        EnsureArm();
        armRenderer.material.color = skinId == "turtle"
            ? new Color(0.16f, 0.55f, 0.20f)
            : skinId == "sun_ducker" ? Color.black : Color.white;
    }

    public void ShowThrow(Vector3 worldDirection)
    {
        EnsureArm();
        Vector3 localDirection = transform.InverseTransformDirection(worldDirection);
        if (localDirection.sqrMagnitude < 0.0001f)
            localDirection = Vector3.forward;

        arm.localPosition = new Vector3(0.16f, -0.20f, 0.58f);
        arm.localRotation = Quaternion.FromToRotation(Vector3.right, localDirection.normalized);
        arm.gameObject.SetActive(true);
        throwEndsAt = Time.time + ThrowDuration;
    }

    public void ShowTeleport()
    {
        EnsureArm();
        teleportStartedAt = Time.time;
        arm.gameObject.SetActive(true);
    }

    public void SetMovementActive(bool active)
    {
        movementActive = active;
    }

    public void SetGrappleActive(bool active, Vector3 worldDirection)
    {
        EnsureArm();
        grappleActive = active;
        if (!active)
            return;
        Vector3 localDirection = transform.InverseTransformDirection(worldDirection);
        arm.localPosition = new Vector3(0.18f, -0.20f, 0.62f);
        arm.localRotation = Quaternion.FromToRotation(Vector3.right, localDirection.normalized);
        arm.gameObject.SetActive(true);
    }

    public Vector3 GrappleOrigin
    {
        get
        {
            EnsureArm();
            return arm.position + arm.right * 0.18f;
        }
    }

    public void Hide()
    {
        throwEndsAt = 0f;
        teleportStartedAt = -1f;
        movementActive = false;
        grappleActive = false;
        if (arm != null)
            arm.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (arm == null)
            return;

        if (teleportStartedAt >= 0f)
        {
            float progress = Mathf.Clamp01((Time.time - teleportStartedAt) / TeleportDuration);
            arm.localPosition = Vector3.Lerp(
                new Vector3(0.50f, -0.16f, 0.55f),
                new Vector3(-0.38f, -0.30f, 0.55f), progress);
            arm.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-25f, 35f, progress));
            if (progress >= 1f)
                teleportStartedAt = -1f;
        }
        else if (grappleActive)
        {
            arm.localPosition = new Vector3(0.18f, -0.20f, 0.62f);
        }
        else if (movementActive)
        {
            arm.localPosition = new Vector3(0.46f, -0.42f, 0.64f);
            arm.localRotation = Quaternion.Euler(12f, 0f, -18f);
        }

        bool visible = teleportStartedAt >= 0f || grappleActive || movementActive || Time.time < throwEndsAt;
        if (arm.gameObject.activeSelf != visible)
            arm.gameObject.SetActive(visible);
    }

    private void EnsureArm()
    {
        if (arm != null)
            return;

        GameObject armObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armObject.name = "LocalFirstPersonArm";
        Destroy(armObject.GetComponent<Collider>());
        arm = armObject.transform;
        arm.SetParent(transform, false);
        arm.localScale = new Vector3(0.34f, 0.11f, 0.11f);
        armRenderer = armObject.GetComponent<Renderer>();
        arm.gameObject.SetActive(false);
    }
}
