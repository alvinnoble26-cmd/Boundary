using UnityEngine;

// Runtime-only local camera presentation. This is deliberately not a networked
// object and does not need a prefab reference, keeping it out of third-person
// skin synchronization and Player.prefab serialization.
public sealed class FirstPersonArmPresentation : MonoBehaviour
{
    private const float ThrowDuration = 0.16f;
    private const float TeleportDuration = 0.5f;
    private const float GrappleLaunchDuration = 0.12f;
    private const float GrapplePullDuration = 0.18f;

    private Transform arm;
    private Renderer armRenderer;
    private Transform secondArm;
    private Renderer secondArmRenderer;
    private float throwEndsAt;
    private float teleportStartedAt = -1f;
    private float grappleLaunchStartedAt = -1f;
    private float grapplePullStartedAt = -1f;
    private Quaternion grappleAimRotation = Quaternion.identity;
    private bool grapplePullActive;
    private bool movementActive;
    private bool grappleActive;
    private bool hollowActive;
    private Vector3 hollowWorldTarget;
    private string skinId = "beard";

    public void SetSkin(string value)
    {
        skinId = value == "turtle" || value == "sun_ducker" ? value : "beard";
        EnsureArm();
        Color color = skinId == "turtle"
            ? new Color(0.16f, 0.55f, 0.20f)
            : skinId == "sun_ducker" ? Color.black : Color.white;
        armRenderer.material.color = color;
        armRenderer.material.SetColor("_Color", color);
        EnsureSecondArm();
        secondArmRenderer.material.color = color;
        secondArmRenderer.material.SetColor("_Color", color);
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

    public void SetHollowActive(bool active, Vector3 worldTarget)
    {
        EnsureArm();
        EnsureSecondArm();
        hollowActive = active;
        if (!active)
        {
            secondArm.gameObject.SetActive(false);
            return;
        }

        hollowWorldTarget = worldTarget;
        arm.gameObject.SetActive(true);
        secondArm.gameObject.SetActive(true);
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
        {
            grappleLaunchStartedAt = -1f;
            grapplePullStartedAt = -1f;
            grapplePullActive = false;
            return;
        }
        Vector3 localDirection = transform.InverseTransformDirection(worldDirection);
        if (localDirection.sqrMagnitude < 0.0001f)
            localDirection = Vector3.forward;
        grappleAimRotation = Quaternion.FromToRotation(Vector3.right, localDirection.normalized);
        grappleLaunchStartedAt = Time.time;
        grapplePullStartedAt = -1f;
        grapplePullActive = false;
        arm.gameObject.SetActive(true);
    }

    public void PlayGrappleYank()
    {
        EnsureArm();
        grapplePullStartedAt = Time.time;
        grapplePullActive = true;
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
        grappleLaunchStartedAt = -1f;
        grapplePullStartedAt = -1f;
        grapplePullActive = false;
        movementActive = false;
        grappleActive = false;
        hollowActive = false;
        if (arm != null)
            arm.gameObject.SetActive(false);
        if (secondArm != null)
            secondArm.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (arm == null)
            return;

        if (hollowActive)
        {
            float pulse = Mathf.Sin(Time.time * 8f) * 0.018f;
            arm.localPosition = new Vector3(0.31f + pulse, -0.27f, 0.57f);
            secondArm.localPosition = new Vector3(-0.31f - pulse, -0.27f, 0.57f);
            Vector3 localTarget = transform.InverseTransformPoint(hollowWorldTarget);
            Vector3 rightDirection = localTarget - arm.localPosition;
            Vector3 leftDirection = localTarget - secondArm.localPosition;
            if (rightDirection.sqrMagnitude < 0.0001f)
                rightDirection = Vector3.forward;
            if (leftDirection.sqrMagnitude < 0.0001f)
                leftDirection = Vector3.forward;
            arm.localRotation = Quaternion.FromToRotation(Vector3.right, rightDirection.normalized) *
                Quaternion.Euler(0f, 0f, -5f);
            secondArm.localRotation = Quaternion.FromToRotation(Vector3.right, leftDirection.normalized) *
                Quaternion.Euler(0f, 0f, 5f);
        }
        else if (teleportStartedAt >= 0f)
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
            Vector3 restingPosition = new Vector3(0.18f, -0.20f, 0.62f);
            Vector3 extendedPosition = new Vector3(0.70f, -0.16f, 0.82f);
            Vector3 pullingPosition = new Vector3(-0.24f, -0.34f, 0.46f);
            if (!grapplePullActive)
            {
                float launchProgress = Mathf.Clamp01((Time.time - grappleLaunchStartedAt) / GrappleLaunchDuration);
                arm.localPosition = Vector3.Lerp(restingPosition, extendedPosition, launchProgress);
                arm.localRotation = Quaternion.Slerp(Quaternion.identity, grappleAimRotation, launchProgress);
            }
            else
            {
                float pullProgress = Mathf.Clamp01((Time.time - grapplePullStartedAt) / GrapplePullDuration);
                arm.localPosition = Vector3.Lerp(extendedPosition, pullingPosition, pullProgress);
                arm.localRotation = Quaternion.Slerp(grappleAimRotation, Quaternion.Euler(0f, 0f, 52f), pullProgress);
            }
        }
        else if (movementActive)
        {
            arm.localPosition = new Vector3(0.46f, -0.42f, 0.64f);
            arm.localRotation = Quaternion.Euler(12f, 0f, -18f);
        }

        bool visible = hollowActive || teleportStartedAt >= 0f || grappleActive || movementActive || Time.time < throwEndsAt;
        if (arm.gameObject.activeSelf != visible)
            arm.gameObject.SetActive(visible);
        if (secondArm != null && secondArm.gameObject.activeSelf != hollowActive)
            secondArm.gameObject.SetActive(hollowActive);
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
        armRenderer.material = CreateMobileSafeMaterial();
        arm.gameObject.SetActive(false);
    }

    private void EnsureSecondArm()
    {
        if (secondArm != null)
            return;

        GameObject armObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armObject.name = "LocalFirstPersonArmLeft";
        Destroy(armObject.GetComponent<Collider>());
        secondArm = armObject.transform;
        secondArm.SetParent(transform, false);
        secondArm.localScale = new Vector3(0.34f, 0.11f, 0.11f);
        secondArmRenderer = armObject.GetComponent<Renderer>();
        secondArmRenderer.material = CreateMobileSafeMaterial();
        secondArm.gameObject.SetActive(false);
    }

    private static Material CreateMobileSafeMaterial()
    {
        // Sprite/UI shaders are already retained by the mobile player because
        // the game uses Unity UI. This avoids a runtime-only URP shader being
        // stripped from an iOS build and rendering the arm pink.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("UI/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        return shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
    }
}
